using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Domain.Enums;

namespace ServerManager.Infrastructure.Monitoring;

public class UptimeProbe : IUptimeProbe
{
    public const string HttpClientName = "ServerManager.Uptime";
    public const int MaxRedirects = 5;

    private readonly IHttpClientFactory _httpClientFactory;

    public UptimeProbe(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public static void ConfigureClient(HttpClient client)
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ServerManager-Uptime", "1.0"));
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = MaxRedirects,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var socket = await NetworkTargetGuard.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
    };

    public async Task<UptimeProbeResult> ProbeAsync(UptimeProbeRequest request, CancellationToken cancellationToken = default)
    {
        var timeoutSeconds = Math.Clamp(request.TimeoutSeconds, 1, 60);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return request.Type == UptimeCheckType.Tcp
                ? await ProbeTcpAsync(request, stopwatch, timeout.Token)
                : await ProbeHttpAsync(request, stopwatch, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Down(stopwatch, null, $"Zaman aşımı ({timeoutSeconds} sn).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Down(stopwatch, null, Describe(ex));
        }
    }

    private async Task<UptimeProbeResult> ProbeHttpAsync(UptimeProbeRequest request, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        if (!NetworkTargets.TryValidateUrl(request.Url, out var error))
            return Down(stopwatch, null, error ?? "Geçersiz adres.");

        var acceptedCodes = StatusCodeRanges.TryParse(request.AcceptedStatusCodes, out _)
            ? request.AcceptedStatusCodes ?? StatusCodeRanges.Default
            : StatusCodeRanges.Default;
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Get, request.Url);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        stopwatch.Stop();

        var code = (int)response.StatusCode;
        var accepted = StatusCodeRanges.IsAccepted(acceptedCodes, code);
        var text = accepted
            ? $"HTTP {code}"
            : $"Beklenmeyen durum kodu: HTTP {code} (kabul: {acceptedCodes}).";
        return new UptimeProbeResult(accepted, Elapsed(stopwatch), code, text);
    }

    private static async Task<UptimeProbeResult> ProbeTcpAsync(UptimeProbeRequest request, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Host) || !NetworkTargets.IsValidHost(request.Host) || request.Port is not (>= 1 and <= 65535))
            return Down(stopwatch, null, "Geçersiz sunucu adresi veya port.");

        using var socket = await NetworkTargetGuard.ConnectAsync(request.Host, request.Port.Value, cancellationToken);
        stopwatch.Stop();
        return new UptimeProbeResult(true, Elapsed(stopwatch), null, $"TCP {request.Port} açık.");
    }

    private static UptimeProbeResult Down(Stopwatch stopwatch, int? statusCode, string message)
    {
        stopwatch.Stop();
        return new UptimeProbeResult(false, Elapsed(stopwatch), statusCode, message);
    }

    private static int Elapsed(Stopwatch stopwatch) =>
        (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);

    public static string Describe(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is BlockedNetworkTargetException)
                return current.Message;
        }

        if (ex is HttpRequestException http)
        {
            return http.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "Alan adı çözümlenemedi.",
                HttpRequestError.ConnectionError => ConnectionMessage(http),
                HttpRequestError.SecureConnectionError => "SSL/TLS bağlantısı kurulamadı (sertifika geçersiz veya süresi dolmuş olabilir).",
                HttpRequestError.InvalidResponse or HttpRequestError.ResponseEnded => "Sunucu geçersiz veya eksik yanıt döndü.",
                _ when http.Message.Contains("redirect", StringComparison.OrdinalIgnoreCase) => $"En fazla {MaxRedirects} yönlendirme izlenir.",
                _ => "HTTP isteği başarısız oldu."
            };
        }

        if (ex is SocketException socket)
            return SocketMessage(socket);

        return "Kontrol başarısız oldu.";
    }

    private static string ConnectionMessage(HttpRequestException ex) =>
        ex.InnerException is SocketException socket ? SocketMessage(socket) : "Bağlantı kurulamadı.";

    private static string SocketMessage(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => "Bağlantı reddedildi (port kapalı).",
        SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => "Alan adı çözümlenemedi.",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => "Hedefe ulaşılamıyor.",
        SocketError.TimedOut => "Bağlantı zaman aşımına uğradı.",
        _ => "Bağlantı kurulamadı."
    };
}

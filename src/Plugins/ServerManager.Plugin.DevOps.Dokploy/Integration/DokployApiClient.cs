using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;
using ServerManager.Plugin.DevOps.Dokploy.Services;

namespace ServerManager.Plugin.DevOps.Dokploy.Integration;

public sealed class DokployApiClient : IDokployApiClient
{
    public const string HttpClientName = "Dokploy";
    private const string ApiKeyHeader = "x-api-key";
    private const int MaxResponseBytes = 4 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DokployApiClient> _logger;

    public DokployApiClient(IHttpClientFactory httpClientFactory, ILogger<DokployApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DokployHttpProbeResult> ProbeHealthAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await SendAsync(baseUrl, "/api/health", apiKey: null, cancellationToken);
        var elapsed = (int)stopwatch.ElapsedMilliseconds;
        if (response.Error is not null)
            return new DokployHttpProbeResult(false, null, response.Error);

        if (response.StatusCode != HttpStatusCode.OK)
            return new DokployHttpProbeResult(false, elapsed, $"Panel HTTP {(int)response.StatusCode} döndürdü.");

        return DokployOutputParser.IsHealthyResponse(response.Body)
            ? new DokployHttpProbeResult(true, elapsed, "Panel yanıt veriyor.")
            : new DokployHttpProbeResult(false, elapsed, "Panel yanıt verdi ancak sağlık kontrolü başarısız.");
    }

    public async Task<ServiceResult<string>> GetVersionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(baseUrl, "/api/settings.getDokployVersion", apiKey, cancellationToken);
        var failure = ToFailure<string>(response);
        if (failure is not null)
            return failure;

        var version = DokployProjectParser.ParseVersion(response.Body);
        return version is null
            ? ServiceResult<string>.Failure("Dokploy sürüm bilgisi okunamadı.")
            : ServiceResult<string>.Success(version);
    }

    public async Task<ServiceResult<IReadOnlyList<DokployProjectDto>>> GetProjectsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(baseUrl, "/api/project.all", apiKey, cancellationToken);
        var failure = ToFailure<IReadOnlyList<DokployProjectDto>>(response);
        if (failure is not null)
            return failure;

        var projects = DokployProjectParser.Parse(response.Body);
        return projects is null
            ? ServiceResult<IReadOnlyList<DokployProjectDto>>.Failure("Dokploy proje listesi okunamadı.")
            : ServiceResult<IReadOnlyList<DokployProjectDto>>.Success(projects);
    }

    private static ServiceResult<T>? ToFailure<T>(DokployHttpResponse response)
    {
        if (response.Error is not null)
            return ServiceResult<T>.Failure(response.Error);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => null,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                ServiceResult<T>.Failure("API anahtarı geçersiz veya yetkisiz.", ServiceErrorType.Forbidden),
            HttpStatusCode.NotFound =>
                ServiceResult<T>.Failure("Dokploy API uç noktası bulunamadı; panel sürümü bu özelliği desteklemiyor olabilir."),
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest =>
                ServiceResult<T>.Failure("Panel başka bir adrese yönlendiriyor; panel adresini (http/https) kontrol edin."),
            _ => ServiceResult<T>.Failure($"Dokploy API HTTP {(int)response.StatusCode} döndürdü.")
        };
    }

    private async Task<DokployHttpResponse> SendAsync(string baseUrl, string path, string? apiKey, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + path, UriKind.Absolute, out var uri))
            return DokployHttpResponse.Failed("Panel adresi geçersiz.");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/json");
        if (apiKey is not null)
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, apiKey);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await ReadBodyAsync(response, cancellationToken);
            return body is null
                ? DokployHttpResponse.Failed("Dokploy yanıtı çok büyük.")
                : new DokployHttpResponse(response.StatusCode, body, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DokployHttpResponse.Failed("Panel zamanında yanıt vermedi.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Dokploy isteği başarısız. Host: {Host}", uri.Host);
            return DokployHttpResponse.Failed(DescribeConnectionError(ex));
        }
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
                return null;

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static string DescribeConnectionError(HttpRequestException ex)
    {
        if (ex.InnerException is SocketException socket)
        {
            return socket.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "Panel bağlantıyı reddetti (port kapalı veya servis çalışmıyor).",
                SocketError.HostNotFound or SocketError.NoData => "Panel adresi çözümlenemedi.",
                SocketError.TimedOut or SocketError.HostUnreachable or SocketError.NetworkUnreachable => "Panele ulaşılamıyor (ağ/güvenlik duvarı).",
                _ => "Panele bağlanılamadı."
            };
        }

        return ex.HttpRequestError switch
        {
            HttpRequestError.SecureConnectionError => "TLS bağlantısı kurulamadı (sertifika geçersiz olabilir).",
            HttpRequestError.NameResolutionError => "Panel adresi çözümlenemedi.",
            HttpRequestError.ConnectionError => "Panele bağlanılamadı.",
            _ => "Panele bağlanılamadı."
        };
    }
}

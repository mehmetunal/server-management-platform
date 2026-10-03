using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Application.Interfaces.Monitoring;

namespace ServerManager.Infrastructure.Monitoring;

public class TlsCertificateProbe : ISslCertificateProbe
{
    public async Task<SslProbeResult> ProbeAsync(string host, int port, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        if (!NetworkTargets.IsValidHost(host) || port is < 1 or > 65535)
            return Failure("Geçersiz alan adı veya port.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60)));

        string? subject = null, issuer = null, validationError = null;
        DateTime? notBefore = null, notAfter = null;
        string? address = null;

        try
        {
            using var socket = await NetworkTargetGuard.ConnectAsync(host, port, timeout.Token);
            address = (socket.RemoteEndPoint as IPEndPoint)?.Address.ToString();
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            await using var ssl = new SslStream(stream, leaveInnerStreamOpen: false);

            var options = new SslClientAuthenticationOptions
            {
                TargetHost = host,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                {
                    if (certificate is not null)
                    {
                        using var cert = new X509Certificate2(certificate);
                        subject = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                        issuer = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: true);
                        notBefore = cert.NotBefore.ToUniversalTime();
                        notAfter = cert.NotAfter.ToUniversalTime();
                    }

                    validationError = DescribeErrors(errors, chain);
                    return true;
                }
            };

            await ssl.AuthenticateAsClientAsync(options, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (notAfter is null)
                return Failure("Zaman aşımı; sertifika okunamadı.", address);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (notAfter is null)
                return Failure(Describe(ex), address);
        }

        if (notAfter is null)
            return Failure("Sunucu sertifika göndermedi.", address);

        return new SslProbeResult(subject, issuer, notBefore, notAfter, address, validationError, null);
    }

    private static SslProbeResult Failure(string error, string? address = null) =>
        new(null, null, null, null, address, null, error);

    public static string? DescribeErrors(SslPolicyErrors errors, X509Chain? chain)
    {
        if (errors == SslPolicyErrors.None)
            return null;

        var messages = new List<string>();
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            messages.Add("Sertifika alınamadı.");
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            messages.Add("Sertifika bu alan adı için düzenlenmemiş.");
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            var statuses = chain?.ChainStatus
                .Select(s => s.Status)
                .Where(s => s != X509ChainStatusFlags.NoError)
                .Distinct()
                .Select(ChainStatusText)
                .ToList() ?? [];
            messages.Add(statuses.Count == 0
                ? "Sertifika zinciri doğrulanamadı."
                : $"Sertifika zinciri doğrulanamadı: {string.Join(", ", statuses)}.");
        }

        return string.Join(" ", messages);
    }

    private static string ChainStatusText(X509ChainStatusFlags flag) => flag switch
    {
        X509ChainStatusFlags.NotTimeValid => "süresi geçmiş veya henüz geçerli değil",
        X509ChainStatusFlags.UntrustedRoot => "güvenilmeyen kök (self-signed olabilir)",
        X509ChainStatusFlags.PartialChain => "ara sertifika eksik",
        X509ChainStatusFlags.Revoked => "iptal edilmiş",
        X509ChainStatusFlags.NotSignatureValid => "imza geçersiz",
        _ => flag.ToString()
    };

    private static string Describe(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case BlockedNetworkTargetException:
                    return current.Message;
                case SocketException socket:
                    return socket.SocketErrorCode switch
                    {
                        SocketError.ConnectionRefused => "Bağlantı reddedildi (port kapalı).",
                        SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => "Alan adı çözümlenemedi.",
                        SocketError.TimedOut => "Bağlantı zaman aşımına uğradı.",
                        _ => "Bağlantı kurulamadı."
                    };
                case AuthenticationException:
                    return "TLS el sıkışması başarısız (port TLS konuşmuyor olabilir).";
                case IOException:
                    return "Sunucu bağlantıyı kapattı (port TLS konuşmuyor olabilir).";
            }
        }

        return "Sertifika okunamadı.";
    }
}

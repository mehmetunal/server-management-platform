using ServerManager.Application.DTOs.Ssl;

namespace ServerManager.Application.Interfaces.Monitoring;

/// <summary>TLS el sıkışmasıyla sunucunun sertifikasını okur; hata fırlatmaz.</summary>
public interface ISslCertificateProbe
{
    Task<SslProbeResult> ProbeAsync(string host, int port, int timeoutSeconds, CancellationToken cancellationToken = default);
}

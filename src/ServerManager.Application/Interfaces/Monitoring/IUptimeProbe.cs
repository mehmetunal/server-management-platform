using ServerManager.Application.DTOs.Uptime;

namespace ServerManager.Application.Interfaces.Monitoring;

/// <summary>HTTP/TCP erişilebilirlik kontrolü; panel sunucusundan yapılır ve hata fırlatmaz.</summary>
public interface IUptimeProbe
{
    Task<UptimeProbeResult> ProbeAsync(UptimeProbeRequest request, CancellationToken cancellationToken = default);
}

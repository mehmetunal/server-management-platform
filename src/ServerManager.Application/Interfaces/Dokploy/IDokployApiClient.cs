using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Dokploy;

namespace ServerManager.Application.Interfaces.Dokploy;

public interface IDokployApiClient
{
    /// <summary>Kimlik doğrulama gerektirmeyen /api/health uç noktasını panelden çağırır.</summary>
    Task<DokployHttpProbeResult> ProbeHealthAsync(string baseUrl, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> GetVersionAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DokployProjectDto>>> GetProjectsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);
}

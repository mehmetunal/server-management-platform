using ServerManager.Application.Common;
using ServerManager.Application.Dokploy;
using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Dokploy;

/// <summary>Sunucu üzerindeki Dokploy işlemleri (SSH). HTTP API için <see cref="IDokployApiClient"/> kullanılır.</summary>
public interface IDokployProvider
{
    Task<ServiceResult<DokployHostFacts>> GatherFactsAsync(RemoteExecutionContext context, DokployOptions options, CancellationToken cancellationToken = default);

    Task<ServiceResult<DokployHostStatusDto>> GetStatusAsync(RemoteExecutionContext context, int port, CancellationToken cancellationToken = default);

    Task<bool> IsLocallyHealthyAsync(RemoteExecutionContext context, int port, CancellationToken cancellationToken = default);

    /// <summary>Betiği indirir, özetini çıkarır ve çalıştırır; çıktı geldiği anda <paramref name="observer"/>'a iletilir.</summary>
    Task<ServiceResult<DokployScriptResult>> RunInstallScriptAsync(
        RemoteExecutionContext context,
        DokployInstallPlan plan,
        IDokployInstallObserver observer,
        CancellationToken cancellationToken = default);
}

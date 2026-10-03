using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Services;

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

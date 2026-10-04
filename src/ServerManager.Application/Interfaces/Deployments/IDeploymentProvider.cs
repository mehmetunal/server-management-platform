using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Deployments;

/// <summary>Deployment adımlarını hedef sunucuda (SSH üzerinden git ve Docker CLI ile) çalıştırır.</summary>
public interface IDeploymentProvider
{
    /// <summary>Depoya hedef sunucudan erişilebildiğini doğrular ve dalları listeler.</summary>
    Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(
        RemoteExecutionContext context,
        GitSource source,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DeploymentRunResult>> DeployAsync(
        RemoteExecutionContext context,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProxyStatusDto>> GetProxyStatusAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult> InstallProxyAsync(RemoteExecutionContext context, string acmeEmail, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Yeniden build etmeden Traefik etiketlerini ve özel sertifikaları uygular.</summary>
    Task<ServiceResult> ApplyRoutingAsync(
        RemoteExecutionContext context,
        DeploymentPlan plan,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>Projenin sunucudaki klasörünü, container'ını, imajını, volume'larını ve vekil dosyalarını siler.</summary>
    Task<ServiceResult> RemoveDeploymentAsync(
        RemoteExecutionContext context,
        DeploymentPlan plan,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

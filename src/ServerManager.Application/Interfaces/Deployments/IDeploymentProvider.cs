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
}

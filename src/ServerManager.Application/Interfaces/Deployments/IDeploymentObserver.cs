using ServerManager.Application.Deployments;

namespace ServerManager.Application.Interfaces.Deployments;

public interface IDeploymentObserver
{
    Task OnStageAsync(DeploymentStage stage, string message, CancellationToken cancellationToken);

    Task OnOutputAsync(string text, CancellationToken cancellationToken);

    Task OnCommitAsync(DeploymentCommit commit, CancellationToken cancellationToken);
}

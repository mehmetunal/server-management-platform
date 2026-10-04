using ServerManager.Application.Interfaces.Deployments;

namespace ServerManager.Application.Deployments;

/// <summary>Yönlendirme uygulaması deployment logu yazmaz.</summary>
public sealed class NullDeploymentObserver : IDeploymentObserver
{
    public static readonly NullDeploymentObserver Instance = new();

    private NullDeploymentObserver()
    {
    }

    public Task OnStageAsync(DeploymentStage stage, string message, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnOutputAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnCommitAsync(DeploymentCommit commit, CancellationToken cancellationToken) => Task.CompletedTask;
}

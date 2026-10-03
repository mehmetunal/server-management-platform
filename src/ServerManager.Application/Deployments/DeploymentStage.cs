namespace ServerManager.Application.Deployments;

/// <summary>Canlı deployment ekranındaki adım; kalıcı durum <see cref="ServerManager.Domain.Enums.DeploymentStatus"/>'tadır.</summary>
public enum DeploymentStage
{
    Preparing,
    Source,
    Building,
    Deploying,
    Completed,
    Failed,
    Cancelled
}

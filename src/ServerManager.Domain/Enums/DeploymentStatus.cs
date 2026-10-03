namespace ServerManager.Domain.Enums;

public enum DeploymentStatus
{
    Started = 0,
    Building = 1,
    Deploying = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    Interrupted = 6
}

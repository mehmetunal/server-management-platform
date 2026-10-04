namespace ServerManager.Domain.Enums;

public enum BackupRunStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Interrupted = 5
}

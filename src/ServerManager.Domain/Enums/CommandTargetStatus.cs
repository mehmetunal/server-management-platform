namespace ServerManager.Domain.Enums;

public enum CommandTargetStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    TimedOut = 4,
    Interrupted = 5
}

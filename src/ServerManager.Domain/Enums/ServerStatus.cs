namespace ServerManager.Domain.Enums;

public enum ServerStatus
{
    Unknown = 0,
    Healthy = 1,
    Warning = 2,
    Critical = 3,
    Offline = 4,
    Maintenance = 5
}

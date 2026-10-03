namespace ServerManager.Plugin.DevOps.Dokploy.Domain;

public enum DokployStatus
{
    Unknown = 0,
    NotInstalled = 1,
    Running = 2,
    Degraded = 3,
    Stopped = 4,
    Unreachable = 5
}

namespace ServerManager.Application.Dokploy;

public enum DokployInstallStage
{
    Checking = 0,
    Downloading = 1,
    Installing = 2,
    HealthCheck = 3,
    Completed = 4,
    Failed = 5
}

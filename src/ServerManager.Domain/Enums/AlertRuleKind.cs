namespace ServerManager.Domain.Enums;

public enum AlertRuleKind
{
    CpuUsage = 1,
    MemoryUsage = 2,
    DiskUsage = 3,
    ServerOffline = 4,
    UptimeCheckDown = 5,
    SslCertificateExpiry = 6,
    DeploymentFailed = 7,
    BackupFailed = 8,
    SecurityFinding = 9
}

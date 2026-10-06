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
    SecurityFinding = 9,

    /// <summary>Yönetilen servisin container'ı çalışmıyor veya sağlıksız (kaynak geçmişi toplayıcısının okuduğu durum).</summary>
    ServiceDown = 10,

    /// <summary>Container'ın yeniden başlama sayısı pencere içinde eşik kadar arttı (docker inspect RestartCount).</summary>
    ContainerRestartLoop = 11,

    /// <summary>Temizlik taramasında geri kazanılabilir alan eşiği aştı (GB).</summary>
    ReclaimableSpace = 12
}

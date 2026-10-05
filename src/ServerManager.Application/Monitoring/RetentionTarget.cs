namespace ServerManager.Application.Monitoring;

public enum RetentionTarget
{
    RawMetrics = 1,
    HourlyMetrics = 2,
    HealthChecks = 3,
    UptimeResults = 4,
    NotificationDeliveries = 5,
    SecurityScans = 6,

    /// <summary>Biten deployment'ların log metni temizlenir; kayıt silinmez.</summary>
    DeploymentLogs = 7,

    /// <summary>Biten yedek/geri yüklemelerin log metni temizlenir; kayıt silinmez.</summary>
    BackupRunLogs = 8,

    /// <summary>Biten toplu komut çalıştırmaları hedef çıktılarıyla birlikte silinir.</summary>
    CommandRuns = 9,

    /// <summary>Kapanmış terminal oturumları komut geçmişiyle birlikte silinir.</summary>
    TerminalSessions = 10,

    /// <summary>Çözülmüş alarm kayıtları silinir.</summary>
    AlertEvents = 11
}

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
    AlertEvents = 11,

    /// <summary>Biten servis işlemlerinin (kurulum, yükseltme, kaldırma) log metni temizlenir; kayıt silinmez.</summary>
    ServiceOperationLogs = 20,

    /// <summary>Kaynak geçmişi: ham container örnekleri (sunucu başına en yeni satır korunur).</summary>
    ContainerMetrics = 21,

    /// <summary>Kaynak geçmişi: container saatlik özetleri.</summary>
    ContainerMetricsHourly = 22,

    /// <summary>Kaynak geçmişi: process anlık görüntüleri.</summary>
    ProcessSnapshots = 23
}

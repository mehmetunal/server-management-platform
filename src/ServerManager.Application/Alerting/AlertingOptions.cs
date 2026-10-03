namespace ServerManager.Application.Alerting;

public sealed class AlertingOptions
{
    public const string SectionName = "Alerting";

    public bool Enabled { get; set; } = true;

    public int EvaluationIntervalSeconds { get; set; } = 60;

    /// <summary>Bildirimlerdeki panel bağlantısı için dışarıdan görünen adres; boşsa bağlantı eklenmez.</summary>
    public string? PublicBaseUrl { get; set; }

    public int NotificationTimeoutSeconds { get; set; } = 15;

    public int UptimeMinimumIntervalSeconds { get; set; } = 30;

    public int UptimeMaxConcurrency { get; set; } = 8;

    public int UptimeResultRetentionDays { get; set; } = 30;

    public int SslCheckIntervalHours { get; set; } = 6;

    public int SslExpiringDays { get; set; } = 30;

    public int DeliveryRetentionDays { get; set; } = 90;
}

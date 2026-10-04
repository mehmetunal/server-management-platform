namespace ServerManager.Application.Monitoring;

public enum RetentionTarget
{
    RawMetrics = 1,
    HourlyMetrics = 2,
    HealthChecks = 3,
    UptimeResults = 4,
    NotificationDeliveries = 5,
    SecurityScans = 6
}

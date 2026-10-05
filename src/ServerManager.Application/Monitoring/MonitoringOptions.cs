namespace ServerManager.Application.Monitoring;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;

    public int MaxConcurrency { get; set; } = 4;

    public int OfflineAfterFailures { get; set; } = 2;

    public double CpuWarningPercent { get; set; } = 80;

    public double CpuCriticalPercent { get; set; } = 95;

    public double MemoryWarningPercent { get; set; } = 85;

    public double MemoryCriticalPercent { get; set; } = 95;

    public double DiskWarningPercent { get; set; } = 80;

    public double DiskCriticalPercent { get; set; } = 90;

    public int RawRetentionHours { get; set; } = 48;

    /// <summary>Bakımın uyguladığı ham metrik saklama süresi (en az 2 saat).</summary>
    public int EffectiveRawRetentionHours => Math.Max(MinimumRawRetentionHours, RawRetentionHours);

    public const int MinimumRawRetentionHours = 2;

    public int HourlyRetentionDays { get; set; } = 90;

    public int HealthCheckRetentionDays { get; set; } = 30;

    public int MaintenanceIntervalMinutes { get; set; } = 10;
}

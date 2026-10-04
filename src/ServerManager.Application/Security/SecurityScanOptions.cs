namespace ServerManager.Application.Security;

public sealed class SecurityScanOptions
{
    public const string SectionName = "SecurityScan";

    /// <summary>Otomatik tarama aralığı; 0 otomatik taramayı kapatır (elle tarama her zaman çalışır).</summary>
    public int ScanIntervalHours { get; set; } = 24;

    public int RetentionDays { get; set; } = 180;

    /// <summary>Saklama süresi dolsa da her sunucu için korunan son tarama sayısı.</summary>
    public int KeepLatestPerServer { get; set; } = 20;
}

namespace ServerManager.Application.Cloud;

public sealed class CloudOptions
{
    public const string SectionName = "Cloud";

    /// <summary>Otomatik eşitleme aralığı; 0 otomatik eşitlemeyi kapatır (elle eşitleme her zaman çalışır).</summary>
    public int SyncIntervalHours { get; set; } = 6;
}

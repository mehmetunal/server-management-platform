using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupJobListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public Guid StorageId { get; init; }

    public string StorageName { get; init; } = string.Empty;

    public BackupSourceType SourceType { get; init; }

    public string SourceSummary { get; init; } = string.Empty;

    /// <summary>Veritabanı yedeğinde motor; diğer kaynaklarda null.</summary>
    public BackupDatabaseEngine? DatabaseEngine { get; init; }

    /// <summary>Veritabanı container'da çalışıyorsa container adı (ör. yönetilen servis <c>sm-svc-db</c>).</summary>
    public string? ContainerName { get; init; }

    public BackupScheduleType ScheduleType { get; init; }

    public int ScheduleIntervalHours { get; init; }

    public int ScheduleMinuteOfDay { get; init; }

    public DayOfWeek? ScheduleDayOfWeek { get; init; }

    public DateTime? NextRunAt { get; init; }

    public DateTime? LastRunAt { get; init; }

    public BackupRunStatus? LastRunStatus { get; init; }

    /// <summary>Dosyası depolamada duran en yeni başarılı yedek; yoksa null (indirme düğmesi pasif gösterilir).</summary>
    public BackupArtifactRef? LatestBackup { get; init; }

    public bool EncryptionEnabled { get; init; }

    public int KeepLast { get; init; }

    public int KeepDays { get; init; }

    public bool IsEnabled { get; init; }

    public bool IsDeleted { get; init; }
}

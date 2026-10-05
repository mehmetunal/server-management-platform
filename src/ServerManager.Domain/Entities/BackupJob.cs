using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class BackupJob : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid ServerId { get; set; }

    public Guid StorageId { get; set; }

    public BackupSourceType SourceType { get; set; }

    /// <summary>Dosya yedeği: her satırda bir mutlak yol.</summary>
    public string? Paths { get; set; }

    /// <summary>Dosya yedeği: her satırda bir hariç tutma kalıbı (tar --exclude).</summary>
    public string? Excludes { get; set; }

    public string? VolumeName { get; set; }

    public BackupDatabaseEngine? DatabaseEngine { get; set; }

    /// <summary>Veritabanı container'da çalışıyorsa container adı; boşsa sunucuda kurulu istemci kullanılır.</summary>
    public string? ContainerName { get; set; }

    public string? DatabaseName { get; set; }

    public string? DatabaseUser { get; set; }

    /// <summary>MongoDB: kullanıcının tanımlı olduğu kimlik doğrulama veritabanı (boşsa admin).</summary>
    public string? DatabaseAuthSource { get; set; }

    public string? EncryptedDatabasePassword { get; set; }

    public string? DatabaseHost { get; set; }

    public int? DatabasePort { get; set; }

    public bool EncryptionEnabled { get; set; } = true;

    /// <summary>Yedek dosyasını şifreleyen parola; Security:MasterKey ile şifreli saklanır.</summary>
    public string? EncryptedPassphrase { get; set; }

    public BackupScheduleType ScheduleType { get; set; } = BackupScheduleType.Manual;

    public int ScheduleIntervalHours { get; set; } = 24;

    /// <summary>Günlük/haftalık yedekte gece yarısından itibaren dakika (Backup:TimeZone saatine göre).</summary>
    public int ScheduleMinuteOfDay { get; set; } = 180;

    public DayOfWeek? ScheduleDayOfWeek { get; set; }

    public DateTime? NextRunAt { get; set; }

    public int KeepLast { get; set; } = 7;

    /// <summary>0 ise yalnızca <see cref="KeepLast"/> uygulanır.</summary>
    public int KeepDays { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastRunAt { get; set; }

    public BackupRunStatus? LastRunStatus { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Server? Server { get; set; }

    public BackupStorage? Storage { get; set; }
}

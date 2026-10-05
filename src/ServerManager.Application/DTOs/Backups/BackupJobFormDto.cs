using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupJobFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ServerId { get; set; }

    public Guid? StorageId { get; set; }

    public BackupSourceType SourceType { get; set; } = BackupSourceType.Files;

    /// <summary>Her satırda bir mutlak yol.</summary>
    public string? Paths { get; set; }

    public string? Excludes { get; set; }

    public string? VolumeName { get; set; }

    public BackupDatabaseEngine DatabaseEngine { get; set; } = BackupDatabaseEngine.PostgreSql;

    public string? ContainerName { get; set; }

    public string? DatabaseName { get; set; }

    public string? DatabaseUser { get; set; }

    /// <summary>MongoDB: kimlik doğrulama veritabanı; boşsa admin.</summary>
    public string? DatabaseAuthSource { get; set; }

    /// <summary>Yalnızca yazılır; düzenlemede boş bırakılırsa kayıtlı parola korunur.</summary>
    public string? DatabasePassword { get; set; }

    public bool ClearDatabasePassword { get; set; }

    public bool HasStoredDatabasePassword { get; set; }

    public string? DatabaseHost { get; set; }

    public int? DatabasePort { get; set; }

    public bool EncryptionEnabled { get; set; } = true;

    /// <summary>Yalnızca yazılır; düzenlemede boş bırakılırsa kayıtlı parola korunur.</summary>
    public string? Passphrase { get; set; }

    public string? PassphraseConfirm { get; set; }

    public bool HasStoredPassphrase { get; set; }

    public BackupScheduleType ScheduleType { get; set; } = BackupScheduleType.Daily;

    public int ScheduleIntervalHours { get; set; } = 24;

    /// <summary>"SS:dd" biçiminde saat.</summary>
    public string ScheduleTime { get; set; } = "03:00";

    public DayOfWeek ScheduleDayOfWeek { get; set; } = DayOfWeek.Sunday;

    public int KeepLast { get; set; } = 7;

    public int KeepDays { get; set; }

    public bool IsEnabled { get; set; } = true;
}

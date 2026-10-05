using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

/// <summary>Geri yükleme formunun varsayılanları.</summary>
public sealed class BackupRestoreFormDto
{
    public required BackupRunDetailsDto Run { get; init; }

    public Guid DefaultServerId { get; init; }

    public string? DefaultDirectory { get; init; }

    public string? DefaultVolume { get; init; }

    public string? DefaultContainer { get; init; }

    public string? DefaultDatabase { get; init; }

    public BackupDatabaseEngine? DatabaseEngine { get; init; }

    /// <summary>Veritabanı geri yüklemesi için iş (ve kimlik bilgisi) hâlâ kayıtlı mı ve motor panelden geri yüklemeyi destekliyor mu.</summary>
    public bool CanRestoreDatabase { get; init; }

    /// <summary>Panelden geri yüklenemeyen yedekler (ör. Redis) için elle geri yükleme yönergesi.</summary>
    public string? ManualRestoreGuidance { get; init; }
}

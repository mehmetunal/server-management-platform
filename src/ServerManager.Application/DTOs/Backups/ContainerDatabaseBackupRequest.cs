using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

/// <summary>
/// Başka modüllerin (ör. yönetilen servisler) container'da çalışan bir veritabanı için yedekleme işi açması için girdi.
/// <see cref="Interfaces.Services.IBackupJobService.CreateForContainerDatabaseAsync"/> bunu <see cref="BackupJobFormDto"/>'ya çevirir
/// ve normal oluşturma yolundan (doğrulama, parola şifreleme, audit) geçirir.
/// </summary>
/// <param name="Name">İş adı (benzersiz, en fazla 128 karakter).</param>
/// <param name="ServerId">Container'ın çalıştığı sunucu.</param>
/// <param name="StorageId">Yedeklerin yazılacağı depolama hedefi.</param>
/// <param name="Engine">Veritabanı motoru.</param>
/// <param name="ContainerName">Container adı; komutlar <c>docker exec -i</c> ile container içinde çalışır.</param>
/// <param name="DatabaseName">
/// PostgreSQL/MySQL/SQL Server'da zorunlu; MongoDB'de boşsa tüm veritabanları; Redis'te yok sayılır.
/// </param>
/// <param name="User">Boşsa motorun varsayılanı (postgres, root, root, sa; Redis'te kullanıcı yok).</param>
/// <param name="Password">Şifreli saklanır; komut satırına yazılmaz, stdin ile verilir. Boş olabilir.</param>
public sealed record ContainerDatabaseBackupRequest(
    string Name,
    Guid ServerId,
    Guid StorageId,
    BackupDatabaseEngine Engine,
    string ContainerName,
    string? DatabaseName,
    string? User,
    string? Password)
{
    /// <summary>MongoDB kimlik doğrulama veritabanı; boşsa admin.</summary>
    public string? AuthDatabase { get; init; }

    public BackupScheduleType ScheduleType { get; init; } = BackupScheduleType.Daily;

    /// <summary>"SS:dd"; günlük/haftalık zamanlamada kullanılır (Backup:TimeZone saatine göre).</summary>
    public string ScheduleTime { get; init; } = "03:00";

    public int ScheduleIntervalHours { get; init; } = 24;

    public DayOfWeek ScheduleDayOfWeek { get; init; } = DayOfWeek.Sunday;

    public int KeepLast { get; init; } = 7;

    public int KeepDays { get; init; }

    /// <summary>Doluysa (en az 12 karakter) yedek AES-256-GCM ile şifrelenir; boşsa şifreleme kapalıdır.</summary>
    public string? EncryptionPassphrase { get; init; }

    public bool IsEnabled { get; init; } = true;
}

using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Backups;

/// <summary>Diğer modüllerin yedekleme işi tanımı üretmesi için yardımcılar.</summary>
public static class BackupJobSpecs
{
    /// <summary>
    /// Container'daki bir veritabanı için oluşturma formunu doldurur. Kullanıcı boşsa motorun varsayılanı kullanılır;
    /// Redis'te veritabanı adı, MongoDB dışında kimlik doğrulama veritabanı yok sayılır.
    /// </summary>
    public static BackupJobFormDto ForContainerDatabase(ContainerDatabaseBackupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var encrypted = !string.IsNullOrEmpty(request.EncryptionPassphrase);
        var user = string.IsNullOrWhiteSpace(request.User) ? BackupDatabaseEngines.DefaultUser(request.Engine) : request.User.Trim();

        return new BackupJobFormDto
        {
            Name = request.Name,
            ServerId = request.ServerId,
            StorageId = request.StorageId,
            SourceType = BackupSourceType.Database,
            DatabaseEngine = request.Engine,
            ContainerName = request.ContainerName,
            DatabaseName = BackupDatabaseEngines.UsesDatabaseName(request.Engine) ? request.DatabaseName : null,
            DatabaseUser = user,
            DatabasePassword = string.IsNullOrEmpty(request.Password) ? null : request.Password,
            DatabaseAuthSource = BackupDatabaseEngines.UsesAuthSource(request.Engine) ? request.AuthDatabase : null,
            EncryptionEnabled = encrypted,
            Passphrase = encrypted ? request.EncryptionPassphrase : null,
            PassphraseConfirm = encrypted ? request.EncryptionPassphrase : null,
            ScheduleType = request.ScheduleType,
            ScheduleTime = request.ScheduleTime,
            ScheduleIntervalHours = request.ScheduleIntervalHours,
            ScheduleDayOfWeek = request.ScheduleDayOfWeek,
            KeepLast = request.KeepLast,
            KeepDays = request.KeepDays,
            IsEnabled = request.IsEnabled
        };
    }
}

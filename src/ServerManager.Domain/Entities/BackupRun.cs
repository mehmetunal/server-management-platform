using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

/// <summary>Yedekleme veya geri yükleme çalışması. Kayıt silinmez; saklama süresi dolan yedekte yalnızca dosya silinir.</summary>
public class BackupRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public BackupOperation Operation { get; set; } = BackupOperation.Backup;

    public BackupTrigger Trigger { get; set; } = BackupTrigger.Manual;

    public BackupRunStatus Status { get; set; } = BackupRunStatus.Running;

    public Guid? JobId { get; set; }

    public string JobName { get; set; } = string.Empty;

    public BackupSourceType SourceType { get; set; }

    /// <summary>Yedekte kaynak sunucu, geri yüklemede hedef sunucu.</summary>
    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public Guid? StorageId { get; set; }

    public string StorageName { get; set; } = string.Empty;

    public string? ObjectKey { get; set; }

    public string? FileName { get; set; }

    public long? SizeBytes { get; set; }

    public string? Sha256 { get; set; }

    public bool IsEncrypted { get; set; }

    /// <summary>Bu yedeği şifreleyen parola (Security:MasterKey ile şifreli); iş parolası sonradan değişse de eski yedek açılabilir.</summary>
    public string? EncryptedPassphrase { get; set; }

    /// <summary>Geri yüklemede kullanılan yedek çalışması.</summary>
    public Guid? SourceRunId { get; set; }

    /// <summary>Geri yükleme hedefinin açıklaması (klasör, volume veya veritabanı).</summary>
    public string? RestoreTarget { get; set; }

    public string? FailureReason { get; set; }

    public string Log { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? CancelledBy { get; set; }

    public DateTime? ArtifactDeletedAt { get; set; }

    public string? ArtifactDeletedBy { get; set; }
}

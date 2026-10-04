using ServerManager.Domain.Common;

namespace ServerManager.Domain.Entities;

public class BackupStorage : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Depolamayı sağlayan sağlayıcının SystemName'i (ör. Storage.Local, Storage.S3); değiştirilmez.</summary>
    public string ProviderSystemName { get; set; } = string.Empty;

    /// <summary>Sağlayıcı ayarları (JSON), tamamı şifreli.</summary>
    public string EncryptedSettings { get; set; } = string.Empty;

    public DateTime? LastTestedAt { get; set; }

    public string? LastError { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}

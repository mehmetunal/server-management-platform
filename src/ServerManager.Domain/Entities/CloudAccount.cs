using ServerManager.Domain.Common;

namespace ServerManager.Domain.Entities;

public class CloudAccount : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Sağlayıcı eklentisinin SystemName değeri (ör. Cloud.Hetzner).</summary>
    public string Provider { get; set; } = string.Empty;

    public string EncryptedToken { get; set; } = string.Empty;

    public string? AccountLabel { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public string? LastSyncError { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public ICollection<Server> Servers { get; set; } = new List<Server>();
}

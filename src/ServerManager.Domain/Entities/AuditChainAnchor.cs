namespace ServerManager.Domain.Entities;

/// <summary>
/// Audit zincirinin imzalı başı. Her kayıtla aynı transaction içinde güncellenir; doğrulama zincirin sonunun silinmesini,
/// tablonun boşaltılmasını ve imzalama başladıktan sonra imzası silinen kayıtları bununla yakalar.
/// </summary>
public class AuditChainAnchor
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>İlk imzalı kaydın Id'si; bundan sonraki her kayıt imzalı olmalıdır.</summary>
    public long FirstSignedId { get; set; }

    public DateTime SigningStartedAt { get; set; }

    public long LastId { get; set; }

    public string LastHash { get; set; } = string.Empty;

    public long SignedCount { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Yukarıdaki alanların HMAC-SHA256 imzası (hex).</summary>
    public string Signature { get; set; } = string.Empty;
}

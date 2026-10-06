using ServerManager.Domain.Common;

namespace ServerManager.Domain.Entities;

/// <summary>Kullanıcının kişisel API anahtarı. Anahtarın kendisi saklanmaz; yalnızca öneki ve SHA-256 özeti tutulur.</summary>
public class ApiKey : BaseEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary><c>smk_&lt;Prefix&gt;_…</c>; benzersizdir, kaydı bulmak için kullanılır.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Tam anahtarın SHA-256 özeti (küçük harf hex).</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Boşlukla ayrılmış izin listesi (kapsam).</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>Virgülle ayrılmış CIDR listesi; null ise her adresten.</summary>
    public string? AllowedIps { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public string? LastUsedIp { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedBy { get; set; }
}

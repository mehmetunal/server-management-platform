namespace ServerManager.Application.DTOs.ApiKeys;

public sealed class ApiKeyListItemDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string? UserEmail { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Kısaltılmış gösterim: <c>smk_&lt;önek&gt;_…</c>.</summary>
    public string DisplayKey { get; init; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public string? AllowedIps { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public DateTime? LastUsedAt { get; init; }

    public string? LastUsedIp { get; init; }

    public DateTime? RevokedAt { get; init; }

    public string? RevokedBy { get; init; }

    public bool IsExpired { get; init; }

    public bool IsActive => RevokedAt is null && !IsExpired;
}

public sealed class CreateApiKeyDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Gün; null = süresiz (yalnızca ApiKeys:AllowNoExpiry açıksa).</summary>
    public int? LifetimeDays { get; set; } = 90;

    public List<string> Scopes { get; set; } = [];

    /// <summary>Virgül veya satır sonuyla ayrılmış IP / CIDR listesi; boşsa her yerden.</summary>
    public string? AllowedIps { get; set; }
}

/// <summary>Oluşturulan anahtar. <see cref="Token"/> yalnızca bu yanıtta görünür; sonra geri alınamaz.</summary>
public sealed record ApiKeyCreatedDto(Guid Id, string Name, string Token, DateTime? ExpiresAt);

/// <summary>Kimlik doğrulama sonucu; başarılıysa etkin izinler kapsam ∩ kullanıcının o anki izinleridir.</summary>
public sealed record ApiKeyAuthResult(
    bool Succeeded,
    string? FailureReason,
    Guid KeyId = default,
    string? KeyName = null,
    Guid UserId = default,
    string? UserName = null,
    IReadOnlySet<string>? Permissions = null)
{
    public static ApiKeyAuthResult Fail(string reason) => new(false, reason);
}

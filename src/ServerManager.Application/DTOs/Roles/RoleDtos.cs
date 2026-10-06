using ServerManager.Application.Authorization;

namespace ServerManager.Application.DTOs.Roles;

public sealed class RoleListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsBuiltIn { get; init; }

    public bool IsSuperAdmin { get; init; }

    public int UserCount { get; init; }

    public int PermissionCount { get; init; }
}

/// <summary>Rol oluşturma / düzenleme formu.</summary>
public sealed class RoleFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = [];

    /// <summary>Kullanıcı kendi rolünden kendi yönetim yetkisini kaldırırken uyarıyı onayladı.</summary>
    public bool ConfirmSelfLockout { get; set; }
}

/// <summary>Düzenleme sayfasının görünümü: form + izin kataloğu ve rolün durumu.</summary>
public sealed class RoleEditorDto
{
    public RoleFormDto Form { get; init; } = new();

    public bool IsBuiltIn { get; init; }

    public bool IsSuperAdmin { get; init; }

    public int UserCount { get; init; }

    public IReadOnlyList<PermissionInfo> Catalog { get; init; } = [];

    /// <summary>Yerleşik rolün varsayılan izinleri; özel rollerde boş.</summary>
    public IReadOnlyList<string> Defaults { get; init; } = [];

    /// <summary>Düzenleyenin verebileceği izinler (SuperAdmin değilse yalnızca kendi izinleri).</summary>
    public IReadOnlySet<string> Grantable { get; init; } = new HashSet<string>();
}

public sealed class RoleOptionDto
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsBuiltIn { get; init; }
}

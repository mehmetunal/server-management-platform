namespace ServerManager.Application.Authorization;

/// <summary>Aktif (pasif veya kilitli olmayan) kullanıcı ve rolleri; kilitlenme denetimi için anlık görüntü.</summary>
public sealed record ActiveUserRoles(Guid UserId, IReadOnlyCollection<string> Roles);

/// <summary>
/// Rol / kullanıcı değişikliklerinin paneli yönetilemez bırakmasını engeller. <see cref="CriticalPermissions"/> içindeki
/// her izin için değişiklikten önce en az bir aktif kullanıcıda olan iznin değişiklikten sonra kimsede kalmaması reddedilir.
/// SuperAdmin rolündeki kullanıcı her izne sahip sayılır.
/// </summary>
public static class RoleLockoutGuard
{
    public static readonly IReadOnlyList<string> CriticalPermissions = [Permissions.UserManage, Permissions.RolesManage];

    /// <summary>Değişiklik sonrası hiçbir aktif kullanıcıda kalmayacak kritik izinler (boşsa değişiklik güvenlidir).</summary>
    public static IReadOnlyList<string> LostPermissions(
        IReadOnlyCollection<ActiveUserRoles> usersBefore,
        IReadOnlyDictionary<string, IReadOnlySet<string>> rolePermissionsBefore,
        IReadOnlyCollection<ActiveUserRoles> usersAfter,
        IReadOnlyDictionary<string, IReadOnlySet<string>> rolePermissionsAfter)
    {
        var lost = new List<string>();
        foreach (var permission in CriticalPermissions)
        {
            if (AnyHolds(usersBefore, rolePermissionsBefore, permission) && !AnyHolds(usersAfter, rolePermissionsAfter, permission))
                lost.Add(permission);
        }

        return lost;
    }

    /// <summary>Kullanıcının rolleri üzerinden izne sahip olup olmadığı.</summary>
    public static bool Holds(IEnumerable<string> roles, IReadOnlyDictionary<string, IReadOnlySet<string>> rolePermissions, string permission) =>
        roles.Any(role =>
            string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || (rolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(permission)));

    /// <summary>
    /// Yetki yükseltmeyi engeller: SuperAdmin olmayan bir rol yöneticisi bir role yalnızca kendisinde olan izinleri ekleyebilir.
    /// Dönen liste eklenemeyecek izinlerdir.
    /// </summary>
    public static IReadOnlyList<string> NotGrantable(IEnumerable<string> addedPermissions, IReadOnlySet<string> actorPermissions, bool actorIsSuperAdmin) =>
        actorIsSuperAdmin
            ? []
            : addedPermissions.Where(p => !actorPermissions.Contains(p)).Distinct(StringComparer.Ordinal).ToList();

    private static bool AnyHolds(IEnumerable<ActiveUserRoles> users, IReadOnlyDictionary<string, IReadOnlySet<string>> rolePermissions, string permission) =>
        users.Any(u => Holds(u.Roles, rolePermissions, permission));
}

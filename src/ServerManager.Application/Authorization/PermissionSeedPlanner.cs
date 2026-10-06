namespace ServerManager.Application.Authorization;

/// <summary>
/// Açılışta / eklenti kurulumunda rollere eklenecek varsayılan izinlerin hesabı. Her rol için "bilinen" (daha önce
/// önerilmiş) izinler tutulur: yalnızca hiç önerilmemiş YENİ izinler eklenir. Yöneticinin bilinçli olarak kaldırdığı bir
/// varsayılan izin bilinen kümede olduğundan bir sonraki açılışta geri eklenmez. SuperAdmin değişmez; eksik her izni alır.
/// </summary>
public static class PermissionSeedPlanner
{
    public sealed record Plan(IReadOnlyList<string> ToGrant, IReadOnlyList<string> ToMarkKnown);

    public static Plan For(
        string roleName,
        IEnumerable<string> defaults,
        IReadOnlySet<string> currentPermissions,
        IReadOnlySet<string> knownPermissions)
    {
        var isSuperAdmin = string.Equals(roleName, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
        var toGrant = new List<string>();
        var toMarkKnown = new List<string>();

        foreach (var permission in defaults.Distinct(StringComparer.Ordinal))
        {
            var known = knownPermissions.Contains(permission);
            if (!known)
                toMarkKnown.Add(permission);

            if (currentPermissions.Contains(permission))
                continue;

            if (!known || isSuperAdmin)
                toGrant.Add(permission);
        }

        return new Plan(toGrant, toMarkKnown);
    }
}

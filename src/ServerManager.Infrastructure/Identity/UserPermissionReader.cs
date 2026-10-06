using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Authorization;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Identity;

/// <summary>Kullanıcının o anki rolleri ve izinleri (rollerin birleşimi; SuperAdmin için tüm tanımlı izinler).</summary>
internal sealed record UserPermissions(IReadOnlyList<string> Roles, IReadOnlySet<string> Permissions, bool IsSuperAdmin)
{
    public static async Task<UserPermissions> LoadAsync(ApplicationDbContext db, IPermissionCatalog catalog, Guid userId, CancellationToken cancellationToken)
    {
        var roleIds = await db.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);
        var roleNames = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).Select(r => r.Name!).ToListAsync(cancellationToken);
        var isSuperAdmin = roleNames.Any(Application.Authorization.Roles.IsSuperAdmin);
        if (isSuperAdmin)
            return new UserPermissions(roleNames, catalog.All.Select(p => p.Name).ToHashSet(StringComparer.Ordinal), true);

        var permissions = await db.RoleClaims.AsNoTracking()
            .Where(c => roleIds.Contains(c.RoleId) && c.ClaimType == Application.Authorization.Permissions.ClaimType && c.ClaimValue != null)
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);

        return new UserPermissions(roleNames, permissions.ToHashSet(StringComparer.Ordinal), false);
    }
}

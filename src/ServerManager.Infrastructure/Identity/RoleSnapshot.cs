using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Authorization;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Identity;

/// <summary>Kilitlenme denetimi için aktif kullanıcıların rolleri ve tüm rollerin izinleri.</summary>
internal sealed record RoleSnapshot(
    IReadOnlyList<ActiveUserRoles> Users,
    Dictionary<string, IReadOnlySet<string>> RolePermissions)
{
    public static async Task<RoleSnapshot> LoadAsync(ApplicationDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(cancellationToken);
        var roleNames = roles.ToDictionary(r => r.Id, r => r.Name ?? string.Empty);

        var claims = await db.RoleClaims.AsNoTracking()
            .Where(c => c.ClaimType == Permissions.ClaimType && c.ClaimValue != null)
            .Select(c => new { c.RoleId, c.ClaimValue })
            .ToListAsync(cancellationToken);
        var rolePermissions = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            rolePermissions[role.Name ?? string.Empty] = claims
                .Where(c => c.RoleId == role.Id)
                .Select(c => c.ClaimValue!)
                .ToHashSet(StringComparer.Ordinal);
        }

        var activeUsers = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= now))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var memberships = await db.UserRoles.AsNoTracking()
            .Where(ur => activeUsers.Contains(ur.UserId))
            .ToListAsync(cancellationToken);

        var users = memberships
            .GroupBy(m => m.UserId)
            .Select(g => new ActiveUserRoles(g.Key, g.Select(m => roleNames.GetValueOrDefault(m.RoleId, string.Empty)).ToList()))
            .ToList();

        return new RoleSnapshot(users, rolePermissions);
    }

    /// <summary>Bir kullanıcının rolleri değiştirilmiş hâli (pasif / kilitliyse listeden çıkarılır).</summary>
    public IReadOnlyList<ActiveUserRoles> WithUser(Guid userId, IReadOnlyCollection<string> roles, bool isActive)
    {
        var list = Users.Where(u => u.UserId != userId).ToList();
        if (isActive)
            list.Add(new ActiveUserRoles(userId, roles));
        return list;
    }
}

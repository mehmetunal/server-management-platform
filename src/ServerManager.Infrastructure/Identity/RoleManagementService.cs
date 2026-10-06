using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Roles;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Identity;

public class RoleManagementService : IRoleManagementService
{
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 256;

    /// <summary>Kendi yönetim yetkisini kaldırma uyarısının hata anahtarı; arayüz bu anahtarı görünce onay sorar.</summary>
    public const string ConfirmSelfLockoutKey = nameof(RoleFormDto.ConfirmSelfLockout);

    private const string NotFoundMessage = "Rol bulunamadı.";

    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IPermissionCatalog _catalog;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserSessionRevoker _sessionRevoker;
    private readonly TimeProvider _timeProvider;

    public RoleManagementService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IPermissionCatalog catalog,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IUserSessionRevoker sessionRevoker,
        TimeProvider timeProvider)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
        _catalog = catalog;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _sessionRevoker = sessionRevoker;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _db.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var userCounts = await _db.UserRoles.AsNoTracking()
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);
        var permissionCounts = await _db.RoleClaims.AsNoTracking()
            .Where(c => c.ClaimType == Permissions.ClaimType)
            .GroupBy(c => c.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        return OrderRoles(roles)
            .Select(r => new RoleListItemDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                Description = r.Description,
                IsBuiltIn = Roles.IsBuiltIn(r.Name),
                IsSuperAdmin = Roles.IsSuperAdmin(r.Name),
                UserCount = userCounts.GetValueOrDefault(r.Id),
                PermissionCount = Roles.IsSuperAdmin(r.Name) ? _catalog.All.Count : permissionCounts.GetValueOrDefault(r.Id)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<RoleOptionDto>> GetRoleOptionsAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _db.Roles.AsNoTracking().ToListAsync(cancellationToken);
        return OrderRoles(roles)
            .Select(r => new RoleOptionDto { Name = r.Name ?? string.Empty, Description = r.Description, IsBuiltIn = Roles.IsBuiltIn(r.Name) })
            .ToList();
    }

    public async Task<ServiceResult<RoleEditorDto>> GetForCreateAsync(Guid? cloneFromId, CancellationToken cancellationToken = default)
    {
        var form = new RoleFormDto();
        if (cloneFromId is { } sourceId)
        {
            var source = await _roleManager.FindByIdAsync(sourceId.ToString());
            if (source is null)
                return ServiceResult<RoleEditorDto>.NotFound(NotFoundMessage);

            form.Name = Truncate($"{source.Name} kopyası", MaxNameLength);
            form.Description = source.Description;
            form.Permissions = (await PermissionsOfAsync(source)).ToList();
        }

        return ServiceResult<RoleEditorDto>.Success(await EditorAsync(form, null, 0, cancellationToken));
    }

    public async Task<ServiceResult<RoleEditorDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return ServiceResult<RoleEditorDto>.NotFound(NotFoundMessage);

        var form = new RoleFormDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            Description = role.Description,
            Permissions = (await PermissionsOfAsync(role)).ToList()
        };
        var userCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == role.Id, cancellationToken);
        return ServiceResult<RoleEditorDto>.Success(await EditorAsync(form, role.Name, userCount, cancellationToken));
    }

    public async Task<ServiceResult<Guid>> CreateAsync(RoleFormDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        var errors = ValidateNameAndDescription(name, dto.Description).ToList();
        var permissions = NormalizePermissions(dto.Permissions, errors);
        if (errors.Count == 0 && await _roleManager.FindByNameAsync(name) is not null)
            errors.Add(new ServiceError(nameof(dto.Name), "Bu adla bir rol zaten var."));
        if (errors.Count > 0)
            return ServiceResult<Guid>.ValidationFailure(errors);

        var actor = await ActorAsync(cancellationToken);
        var notGrantable = RoleLockoutGuard.NotGrantable(permissions, actor.Permissions, actor.IsSuperAdmin);
        if (notGrantable.Count > 0)
            return ServiceResult<Guid>.Failure(NotGrantableMessage(notGrantable), ServiceErrorType.Forbidden);

        var role = new ApplicationRole(name) { Description = TextHelper.NullIfEmpty(dto.Description?.Trim()) };
        var created = await _roleManager.CreateAsync(role);
        if (!created.Succeeded)
            return ServiceResult<Guid>.ValidationFailure(created.Errors.Select(e => new ServiceError(nameof(dto.Name), e.Description)));

        foreach (var permission in permissions)
            await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.RoleCreate,
            AuditEntityTypes.Role,
            role.Id.ToString(),
            role.Name,
            $"{permissions.Count} izin: {string.Join(", ", permissions)}"), cancellationToken);

        return ServiceResult<Guid>.Success(role.Id, "Rol oluşturuldu.");
    }

    public async Task<ServiceResult> UpdateAsync(RoleFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (Roles.IsSuperAdmin(role.Name))
            return ServiceResult.Failure("SuperAdmin rolü değiştirilemez; her izne her zaman sahiptir.", ServiceErrorType.Forbidden);

        var isBuiltIn = Roles.IsBuiltIn(role.Name);
        var name = isBuiltIn ? role.Name! : dto.Name?.Trim() ?? string.Empty;
        var errors = ValidateNameAndDescription(name, dto.Description).ToList();
        var permissions = NormalizePermissions(dto.Permissions, errors);
        if (errors.Count == 0 && !string.Equals(name, role.Name, StringComparison.Ordinal)
            && await _roleManager.FindByNameAsync(name) is { } other && other.Id != role.Id)
            errors.Add(new ServiceError(nameof(dto.Name), "Bu adla bir rol zaten var."));
        if (errors.Count > 0)
            return ServiceResult.ValidationFailure(errors);

        var change = await ApplyPermissionsAsync(role, permissions, dto.ConfirmSelfLockout, cancellationToken);
        if (!change.Result.IsSuccess)
            return change.Result;

        var description = TextHelper.NullIfEmpty(dto.Description?.Trim());
        var previousName = role.Name;
        var metadataChanged = !string.Equals(name, role.Name, StringComparison.Ordinal) || !string.Equals(description, role.Description, StringComparison.Ordinal);
        if (metadataChanged)
        {
            role.Name = name;
            role.Description = description;
            var updated = await _roleManager.UpdateAsync(role);
            if (!updated.Succeeded)
                return ServiceResult.ValidationFailure(updated.Errors.Select(e => new ServiceError(nameof(dto.Name), e.Description)));

            // Rol adı claim'lerde de taşınır; yeniden adlandırmada kullanıcıların oturumu yenilenir.
            if (!string.Equals(previousName, name, StringComparison.Ordinal))
                await RefreshUsersAsync(role.Id, revokeTerminals: false, "Rolünüzün adı değişti.", cancellationToken);

            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.RoleUpdate,
                AuditEntityTypes.Role,
                role.Id.ToString(),
                role.Name,
                string.Equals(previousName, name, StringComparison.Ordinal) ? "Açıklama güncellendi" : $"Ad: {previousName} -> {name}"), cancellationToken);
        }

        return ServiceResult.Success(change.Changed || metadataChanged ? "Rol güncellendi." : "Değişiklik yok.");
    }

    public async Task<ServiceResult> ResetToDefaultAsync(Guid id, bool confirmSelfLockout, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (Roles.IsSuperAdmin(role.Name))
            return ServiceResult.Failure("SuperAdmin rolü değiştirilemez; her izne her zaman sahiptir.", ServiceErrorType.Forbidden);

        if (!Roles.IsBuiltIn(role.Name))
            return ServiceResult.Failure("Yalnızca yerleşik roller varsayılana döndürülebilir.", ServiceErrorType.Conflict);

        var defaults = _catalog.DefaultsFor(role.Name!);
        var change = await ApplyPermissionsAsync(role, defaults.ToHashSet(StringComparer.Ordinal), confirmSelfLockout, cancellationToken);
        if (!change.Result.IsSuccess)
            return change.Result;

        // Varsayılanlar artık bilinen izinlerdir: seeder bunları tekrar önermez.
        var known = await _db.RoleKnownPermissions.Where(k => k.RoleId == role.Id).Select(k => k.Permission).ToListAsync(cancellationToken);
        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        _db.RoleKnownPermissions.AddRange(defaults.Where(p => !knownSet.Contains(p)).Select(p => new RoleKnownPermission { RoleId = role.Id, Permission = p }));
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(change.Changed ? "Rol varsayılan izinlerine döndürüldü." : "Rol zaten varsayılan izinlerinde.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, Guid? reassignToRoleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (Roles.IsBuiltIn(role.Name))
            return ServiceResult.Failure("Yerleşik roller silinemez.", ServiceErrorType.Forbidden);

        var memberIds = await _db.UserRoles.Where(ur => ur.RoleId == role.Id).Select(ur => ur.UserId).ToListAsync(cancellationToken);
        ApplicationRole? target = null;
        if (memberIds.Count > 0)
        {
            if (reassignToRoleId is not { } targetId)
                return ServiceResult.Failure(
                    $"Bu rolde {memberIds.Count} kullanıcı var. Silmeden önce kullanıcıları başka bir role taşıyın.", ServiceErrorType.Conflict);

            target = targetId == role.Id ? null : await _roleManager.FindByIdAsync(targetId.ToString());
            if (target is null)
                return ServiceResult.ValidationFailure("reassignToRoleId", "Kullanıcıların taşınacağı geçerli bir rol seçin.");

            var actor = await ActorAsync(cancellationToken);
            if (!actor.IsSuperAdmin && !actor.Permissions.Contains(Permissions.UserManage))
                return ServiceResult.Failure("Kullanıcıları başka role taşımak için kullanıcı yönetimi yetkisi gerekir.", ServiceErrorType.Forbidden);
            if (Roles.IsSuperAdmin(target.Name) && !actor.IsSuperAdmin)
                return ServiceResult.Failure("Kullanıcıları yalnızca SuperAdmin, SuperAdmin rolüne taşıyabilir.", ServiceErrorType.Forbidden);
        }

        var snapshot = await SnapshotAsync(cancellationToken);
        var usersAfter = snapshot.Users
            .Select(u => u.Roles.Contains(role.Name!, StringComparer.OrdinalIgnoreCase)
                ? new ActiveUserRoles(u.UserId, u.Roles.Where(r => !string.Equals(r, role.Name, StringComparison.OrdinalIgnoreCase)).Append(target!.Name!).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
                : u)
            .ToList();
        var lost = RoleLockoutGuard.LostPermissions(snapshot.Users, snapshot.RolePermissions, usersAfter, snapshot.RolePermissions);
        if (lost.Count > 0)
            return LockoutFailure(lost);

        foreach (var userId in memberIds)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                continue;

            await _userManager.RemoveFromRoleAsync(user, role.Name!);
            if (!await _userManager.IsInRoleAsync(user, target!.Name!))
                await _userManager.AddToRoleAsync(user, target.Name!);
            await _userManager.UpdateSecurityStampAsync(user);
            await _sessionRevoker.RevokeAsync(user.Id.ToString(), "Rolünüz değiştirildi; terminal oturumu kapatıldı.", cancellationToken);

            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.RoleAssign,
                AuditEntityTypes.User,
                user.Id.ToString(),
                user.Email,
                $"Rol: {role.Name} -> {target.Name} (silinen rolden taşındı)"), cancellationToken);
        }

        var deleted = await _roleManager.DeleteAsync(role);
        if (!deleted.Succeeded)
            return ServiceResult.Failure(string.Join(" ", deleted.Errors.Select(e => e.Description)));

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.RoleDelete,
            AuditEntityTypes.Role,
            role.Id.ToString(),
            role.Name,
            memberIds.Count > 0 ? $"{memberIds.Count} kullanıcı {target!.Name} rolüne taşındı" : null), cancellationToken);

        return ServiceResult.Success("Rol silindi.");
    }

    // ---- İç işler ----

    private sealed record PermissionChange(ServiceResult Result, bool Changed);

    private sealed record ActorInfo(Guid? UserId, IReadOnlySet<string> Permissions, IReadOnlyCollection<string> Roles, bool IsSuperAdmin);

    /// <summary>
    /// Rolün izinlerini hedef kümeye eşitler. Yetki yükseltme, son yönetici kilitlenmesi ve kendi yetkisini kaldırma (onaysız)
    /// denetlenir. İzin kaldırılırsa rol üyelerinin security stamp'i yenilenir (oturum en geç 1 dakikada düşer) ve açık
    /// terminalleri kapatılır; yalnızca ekleme yapılırsa claim'ler security stamp doğrulamasında (≤ 1 dk) kendiliğinden yenilenir.
    /// </summary>
    private async Task<PermissionChange> ApplyPermissionsAsync(ApplicationRole role, IReadOnlySet<string> target, bool confirmSelfLockout, CancellationToken cancellationToken)
    {
        var current = await PermissionsOfAsync(role);
        var added = target.Where(p => !current.Contains(p)).Order(StringComparer.Ordinal).ToList();
        var removed = current.Where(p => !target.Contains(p)).Order(StringComparer.Ordinal).ToList();
        if (added.Count == 0 && removed.Count == 0)
            return new PermissionChange(ServiceResult.Success(), false);

        var actor = await ActorAsync(cancellationToken);
        var notGrantable = RoleLockoutGuard.NotGrantable(added, actor.Permissions, actor.IsSuperAdmin);
        if (notGrantable.Count > 0)
            return new PermissionChange(ServiceResult.Failure(NotGrantableMessage(notGrantable), ServiceErrorType.Forbidden), false);

        var snapshot = await SnapshotAsync(cancellationToken);
        var after = new Dictionary<string, IReadOnlySet<string>>(snapshot.RolePermissions, StringComparer.OrdinalIgnoreCase)
        {
            [role.Name!] = target
        };
        var lost = RoleLockoutGuard.LostPermissions(snapshot.Users, snapshot.RolePermissions, snapshot.Users, after);
        if (lost.Count > 0)
            return new PermissionChange(LockoutFailure(lost), false);

        if (!confirmSelfLockout && actor.Roles.Contains(role.Name!, StringComparer.OrdinalIgnoreCase))
        {
            var selfLost = RoleLockoutGuard.CriticalPermissions
                .Where(p => RoleLockoutGuard.Holds(actor.Roles, snapshot.RolePermissions, p) && !RoleLockoutGuard.Holds(actor.Roles, after, p))
                .ToList();
            if (selfLost.Count > 0)
                return new PermissionChange(ServiceResult.ValidationFailure(
                    ConfirmSelfLockoutKey,
                    $"Bu değişiklik kendi hesabınızdan şu yetkileri kaldırır: {string.Join(", ", selfLost.Select(DisplayName))}. Devam etmek için onaylayın."), false);
        }

        foreach (var permission in removed)
            await _roleManager.RemoveClaimAsync(role, new Claim(Permissions.ClaimType, permission));
        foreach (var permission in added)
            await _roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));

        if (removed.Count > 0)
            await RefreshUsersAsync(role.Id, revokeTerminals: true, "Rolünüzün izinleri değişti; terminal oturumu kapatıldı.", cancellationToken);

        var details = new List<string>();
        if (added.Count > 0)
            details.Add("Eklenen: " + string.Join(", ", added));
        if (removed.Count > 0)
            details.Add("Kaldırılan: " + string.Join(", ", removed));

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.RolePermissionsChange,
            AuditEntityTypes.Role,
            role.Id.ToString(),
            role.Name,
            string.Join("; ", details)), cancellationToken);

        return new PermissionChange(ServiceResult.Success(), true);
    }

    private async Task RefreshUsersAsync(Guid roleId, bool revokeTerminals, string reason, CancellationToken cancellationToken)
    {
        var userIds = await _db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync(cancellationToken);
        foreach (var userId in userIds)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                continue;

            await _userManager.UpdateSecurityStampAsync(user);
            if (revokeTerminals)
                await _sessionRevoker.RevokeAsync(user.Id.ToString(), reason, cancellationToken);
        }
    }

    private async Task<RoleEditorDto> EditorAsync(RoleFormDto form, string? existingName, int userCount, CancellationToken cancellationToken)
    {
        var actor = await ActorAsync(cancellationToken);
        var isSuperAdmin = Roles.IsSuperAdmin(existingName);
        if (isSuperAdmin)
            form.Permissions = _catalog.All.Select(p => p.Name).ToList();

        return new RoleEditorDto
        {
            Form = form,
            IsBuiltIn = Roles.IsBuiltIn(existingName),
            IsSuperAdmin = isSuperAdmin,
            UserCount = userCount,
            Catalog = _catalog.All,
            Defaults = Roles.IsBuiltIn(existingName) ? _catalog.DefaultsFor(existingName!) : [],
            Grantable = actor.IsSuperAdmin ? _catalog.All.Select(p => p.Name).ToHashSet(StringComparer.Ordinal) : actor.Permissions
        };
    }

    private async Task<IReadOnlySet<string>> PermissionsOfAsync(ApplicationRole role)
    {
        if (Roles.IsSuperAdmin(role.Name))
            return _catalog.All.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        return (await _roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<ActorInfo> ActorAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return new ActorInfo(null, new HashSet<string>(), [], false);

        var roleIds = await _db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(cancellationToken);
        var roleNames = await _db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name!).ToListAsync(cancellationToken);
        var isSuperAdmin = roleNames.Any(Roles.IsSuperAdmin);
        var permissions = isSuperAdmin
            ? _catalog.All.Select(p => p.Name).ToHashSet(StringComparer.Ordinal)
            : (await _db.RoleClaims
                    .Where(c => roleIds.Contains(c.RoleId) && c.ClaimType == Permissions.ClaimType)
                    .Select(c => c.ClaimValue!)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

        return new ActorInfo(userId, permissions, roleNames, isSuperAdmin);
    }

    private Task<RoleSnapshot> SnapshotAsync(CancellationToken cancellationToken) =>
        RoleSnapshot.LoadAsync(_db, _timeProvider.GetUtcNow(), cancellationToken);

    private HashSet<string> NormalizePermissions(IEnumerable<string>? permissions, List<ServiceError> errors)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in permissions ?? [])
        {
            if (string.IsNullOrWhiteSpace(permission))
                continue;

            if (!_catalog.IsKnown(permission))
            {
                errors.Add(new ServiceError(nameof(RoleFormDto.Permissions), $"Bilinmeyen izin: {permission}"));
                continue;
            }

            set.Add(permission);
        }

        return set;
    }

    private static IEnumerable<ServiceError> ValidateNameAndDescription(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            yield return new ServiceError(nameof(RoleFormDto.Name), "Rol adı zorunludur.");
        else if (name.Length > MaxNameLength)
            yield return new ServiceError(nameof(RoleFormDto.Name), $"Rol adı en fazla {MaxNameLength} karakter olabilir.");
        else if (!name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.'))
            yield return new ServiceError(nameof(RoleFormDto.Name), "Rol adında yalnızca harf, rakam, boşluk, nokta, tire ve alt çizgi kullanılabilir.");

        if (description?.Trim().Length > MaxDescriptionLength)
            yield return new ServiceError(nameof(RoleFormDto.Description), $"Açıklama en fazla {MaxDescriptionLength} karakter olabilir.");
    }

    private string DisplayName(string permission) =>
        _catalog.All.FirstOrDefault(p => p.Name == permission)?.DisplayName ?? permission;

    private string NotGrantableMessage(IReadOnlyList<string> permissions) =>
        "Kendinizde olmayan izinleri bir role veremezsiniz: " + string.Join(", ", permissions.Select(DisplayName));

    private ServiceResult LockoutFailure(IReadOnlyList<string> lost) =>
        ServiceResult.Failure(
            "Bu değişiklikten sonra hiçbir aktif kullanıcıda şu yetkiler kalmaz; panel yönetilemez hâle gelir: "
            + string.Join(", ", lost.Select(DisplayName)),
            ServiceErrorType.Conflict);

    private static IEnumerable<ApplicationRole> OrderRoles(IEnumerable<ApplicationRole> roles) =>
        roles
            .OrderBy(r => Roles.IsBuiltIn(r.Name) ? Roles.All.ToList().FindIndex(b => string.Equals(b, r.Name, StringComparison.OrdinalIgnoreCase)) : int.MaxValue)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

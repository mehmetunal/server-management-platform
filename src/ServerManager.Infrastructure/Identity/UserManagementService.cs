using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Users;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Identity;

public class UserManagementService : IUserManagementService
{
    private const string NotFoundMessage = "Kullanıcı bulunamadı.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateUserDto> _createValidator;
    private readonly IValidator<UpdateUserDto> _updateValidator;
    private readonly IUserSessionRevoker _sessionRevoker;
    private readonly ApplicationDbContext _db;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<CreateUserDto> createValidator,
        IValidator<UpdateUserDto> updateValidator,
        IUserSessionRevoker sessionRevoker,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _sessionRevoker = sessionRevoker;
        _db = db;
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var result = new List<UserListItemDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserListItemDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
                TwoFactorEnabled = user.TwoFactorEnabled,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
        }

        return result;
    }

    public async Task<ServiceResult<UpdateUserDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return ServiceResult<UpdateUserDto>.NotFound(NotFoundMessage);

        var roles = await _userManager.GetRolesAsync(user);
        return ServiceResult<UpdateUserDto>.Success(new UpdateUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles.ToList(),
            IsActive = user.IsActive
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var email = dto.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Email), "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.");

        var roles = await ResolveRolesAsync(dto.Roles, cancellationToken);
        if (roles.Error is not null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Roles), roles.Error);

        if (roles.Names.Contains(Roles.SuperAdmin) && !await CurrentUserIsSuperAdminAsync())
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Roles), "SuperAdmin rolünü yalnızca bir SuperAdmin atayabilir.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = TextHelper.NullIfEmpty(dto.FullName),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
            return ServiceResult<Guid>.ValidationFailure(ToErrors(createResult, nameof(dto.Password)));

        var roleResult = await _userManager.AddToRolesAsync(user, roles.Names);
        if (!roleResult.Succeeded)
            return ServiceResult<Guid>.ValidationFailure(ToErrors(roleResult, nameof(dto.Roles)));

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.UserCreate,
            AuditEntityTypes.User,
            user.Id.ToString(),
            user.Email,
            $"Rol: {string.Join(", ", roles.Names)}"), cancellationToken);

        return ServiceResult<Guid>.Success(user.Id, "Kullanıcı oluşturuldu.");
    }

    public async Task<ServiceResult> UpdateAsync(UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var user = await _userManager.FindByIdAsync(dto.Id.ToString());
        if (user is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var roles = await ResolveRolesAsync(dto.Roles, cancellationToken);
        if (roles.Error is not null)
            return ServiceResult.ValidationFailure(nameof(dto.Roles), roles.Error);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var isSelf = IsCurrentUser(user);
        var rolesToRemove = currentRoles.Where(r => !roles.Names.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        var rolesToAdd = roles.Names.Where(r => !currentRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        var roleChanged = rolesToRemove.Count > 0 || rolesToAdd.Count > 0;
        var activeChanged = user.IsActive != dto.IsActive;

        if (isSelf && (roleChanged || !dto.IsActive))
            return ServiceResult.ValidationFailure(nameof(dto.Roles), "Kendi rolünüzü değiştiremez veya hesabınızı pasifleştiremezsiniz.");

        var losesSuperAdmin = currentRoles.Contains(Roles.SuperAdmin) && (!roles.Names.Contains(Roles.SuperAdmin) || !dto.IsActive);
        if (losesSuperAdmin && await IsLastActiveSuperAdminAsync(user))
            return ServiceResult.ValidationFailure(nameof(dto.Roles), "Sistemde en az bir aktif SuperAdmin kalmalıdır.");

        if (rolesToAdd.Contains(Roles.SuperAdmin) && !await CurrentUserIsSuperAdminAsync())
            return ServiceResult.ValidationFailure(nameof(dto.Roles), "SuperAdmin rolünü yalnızca bir SuperAdmin atayabilir.");

        if (roleChanged || (activeChanged && !dto.IsActive))
        {
            var now = DateTimeOffset.UtcNow;
            var snapshot = await RoleSnapshot.LoadAsync(_db, now, cancellationToken);
            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > now;
            var after = snapshot.WithUser(user.Id, roles.Names, dto.IsActive && !isLocked);
            var lost = RoleLockoutGuard.LostPermissions(snapshot.Users, snapshot.RolePermissions, after, snapshot.RolePermissions);
            if (lost.Count > 0)
                return ServiceResult.ValidationFailure(nameof(dto.Roles),
                    "Bu değişiklikten sonra hiçbir aktif kullanıcıda şu yetkiler kalmaz: " + string.Join(", ", lost.Select(p => Permissions.DisplayNames.GetValueOrDefault(p, p))));
        }

        var email = dto.Email.Trim();
        var previousEmail = user.Email;
        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var owner = await _userManager.FindByEmailAsync(email);
            if (owner is not null && owner.Id != user.Id)
                return ServiceResult.ValidationFailure(nameof(dto.Email), "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.");
        }

        user.Email = email;
        user.UserName = email;
        user.FullName = TextHelper.NullIfEmpty(dto.FullName);
        user.IsActive = dto.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return ServiceResult.ValidationFailure(ToErrors(updateResult, nameof(dto.Email)));

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
                return ServiceResult.ValidationFailure(ToErrors(removeResult, nameof(dto.Roles)));
        }

        if (rolesToAdd.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
                return ServiceResult.ValidationFailure(ToErrors(addResult, nameof(dto.Roles)));
        }

        var passwordChanged = false;
        if (!string.IsNullOrEmpty(dto.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!resetResult.Succeeded)
                return ServiceResult.ValidationFailure(ToErrors(resetResult, nameof(dto.NewPassword)));
            passwordChanged = true;
        }

        if (roleChanged || activeChanged || emailChanged)
            await _userManager.UpdateSecurityStampAsync(user);

        // Security stamp değişikliği cookie'yi en geç doğrulama aralığında düşürür; açık terminaller hemen kapatılır.
        if (roleChanged || (activeChanged && !dto.IsActive) || passwordChanged)
            await _sessionRevoker.RevokeAsync(user.Id.ToString(), RevokeReason(roleChanged, dto.IsActive), cancellationToken);

        var details = new List<string>();
        if (emailChanged)
            details.Add($"E-posta: {previousEmail} -> {email}");
        if (roleChanged)
            details.Add($"Rol: {string.Join(", ", currentRoles)} -> {string.Join(", ", roles.Names)}");
        if (activeChanged)
            details.Add(dto.IsActive ? "Hesap aktifleştirildi" : "Hesap pasifleştirildi");
        if (passwordChanged)
            details.Add("Parola sıfırlandı");

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.UserUpdate,
            AuditEntityTypes.User,
            user.Id.ToString(),
            user.Email,
            details.Count > 0 ? string.Join("; ", details) : null), cancellationToken);

        if (roleChanged)
        {
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.RoleAssign,
                AuditEntityTypes.User,
                user.Id.ToString(),
                user.Email,
                $"Rol: {string.Join(", ", currentRoles)} -> {string.Join(", ", roles.Names)}"), cancellationToken);
        }

        return ServiceResult.Success("Kullanıcı güncellendi.");
    }

    public async Task<ServiceResult> SetLockAsync(Guid id, bool locked, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (IsCurrentUser(user))
            return ServiceResult.Failure("Kendi hesabınızı kilitleyemezsiniz.", ServiceErrorType.Forbidden);

        if (locked && await _userManager.IsInRoleAsync(user, Roles.SuperAdmin) && await IsLastActiveSuperAdminAsync(user))
            return ServiceResult.Failure("Sistemde en az bir aktif SuperAdmin kalmalıdır.", ServiceErrorType.Conflict);

        if (locked)
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _userManager.UpdateSecurityStampAsync(user);
            await _sessionRevoker.RevokeAsync(user.Id.ToString(), "Hesabınız yönetici tarafından kilitlendi; terminal oturumu kapatıldı.", cancellationToken);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        await _auditLogService.LogAsync(new AuditEntry(
            locked ? AuditActions.UserLock : AuditActions.UserUnlock,
            AuditEntityTypes.User,
            user.Id.ToString(),
            user.Email), cancellationToken);

        return ServiceResult.Success(locked ? "Kullanıcı kilitlendi." : "Kullanıcının kilidi açıldı.");
    }

    public async Task<ServiceResult> ResetTwoFactorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (IsCurrentUser(user))
            return ServiceResult.Failure("Kendi iki adımlı doğrulamanızı Hesabım sayfasından yönetin.", ServiceErrorType.Forbidden);

        if (!user.TwoFactorEnabled)
            return ServiceResult.Failure("Bu kullanıcıda iki adımlı doğrulama zaten kapalı.", ServiceErrorType.Conflict);

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
        await _userManager.UpdateSecurityStampAsync(user);
        await _sessionRevoker.RevokeAsync(user.Id.ToString(), "İki adımlı doğrulamanız sıfırlandı; terminal oturumu kapatıldı.", cancellationToken);

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.UserTwoFactorReset,
            AuditEntityTypes.User,
            user.Id.ToString(),
            user.Email), cancellationToken);

        return ServiceResult.Success("İki adımlı doğrulama sıfırlandı. Kullanıcı sonraki girişte yeniden kurabilir.");
    }

    private static string RevokeReason(bool roleChanged, bool isActive) =>
        !isActive ? "Hesabınız pasifleştirildi; terminal oturumu kapatıldı."
        : roleChanged ? "Rolünüz değiştirildi; terminal oturumu kapatıldı."
        : "Parolanız yönetici tarafından sıfırlandı; terminal oturumu kapatıldı.";

    private sealed record ResolvedRoles(IReadOnlyList<string> Names, string? Error);

    /// <summary>Formdan gelen rol adlarını veritabanındaki adlarına eşler; bilinmeyen rol varsa hata döner.</summary>
    private async Task<ResolvedRoles> ResolveRolesAsync(IEnumerable<string> requested, CancellationToken cancellationToken)
    {
        var wanted = requested.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0)
            return new ResolvedRoles([], "En az bir rol seçin.");

        var existing = await _db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(cancellationToken);
        var names = new List<string>();
        foreach (var name in wanted)
        {
            var match = existing.FirstOrDefault(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return new ResolvedRoles([], $"Rol bulunamadı: {name}");
            names.Add(match);
        }

        return new ResolvedRoles(names, null);
    }

    private async Task<bool> CurrentUserIsSuperAdminAsync()
    {
        if (_currentUser.UserId is null)
            return false;

        var current = await _userManager.FindByIdAsync(_currentUser.UserId);
        return current is not null && await _userManager.IsInRoleAsync(current, Roles.SuperAdmin);
    }

    private bool IsCurrentUser(ApplicationUser user) =>
        string.Equals(_currentUser.UserId, user.Id.ToString(), StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsLastActiveSuperAdminAsync(ApplicationUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
        return !superAdmins.Any(u =>
            u.Id != user.Id
            && u.IsActive
            && (!u.LockoutEnd.HasValue || u.LockoutEnd.Value <= now));
    }

    private static IEnumerable<ServiceError> ToErrors(IdentityResult result, string propertyName) =>
        result.Errors.Select(e => new ServiceError(propertyName, e.Description));
}

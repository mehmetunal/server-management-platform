using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Account;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Infrastructure.Identity;

public class AccountService : IAccountService
{
    private const string InvalidCredentialsMessage = "E-posta veya parola hatalı.";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<LoginDto> loginValidator,
        ILogger<AccountService> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _loginValidator = loginValidator;
        _logger = logger;
    }

    public async Task<ServiceResult> SignInAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _loginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await LogFailedAsync(email, "Kullanıcı bulunamadı.", cancellationToken);
            return ServiceResult.Failure(InvalidCredentialsMessage);
        }

        if (!user.IsActive)
        {
            await LogFailedAsync(email, "Pasif kullanıcı.", cancellationToken);
            return ServiceResult.Failure("Hesabınız pasif durumda. Yöneticinizle iletişime geçin.");
        }

        var result = await _signInManager.PasswordSignInAsync(user, dto.Password, dto.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Kullanıcı giriş yaptı: {UserId}", user.Id);
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.Login,
                AuditEntityTypes.User,
                user.Id.ToString(),
                user.Email,
                UserNameOverride: user.Email), cancellationToken);
            return ServiceResult.Success();
        }

        if (result.IsLockedOut)
        {
            await LogFailedAsync(email, "Hesap kilitli.", cancellationToken);
            return ServiceResult.Failure("Çok fazla başarısız deneme yapıldı veya hesap kilitlendi. Daha sonra tekrar deneyin.");
        }

        if (result.RequiresTwoFactor)
        {
            await LogFailedAsync(email, "İki adımlı doğrulama gerekli.", cancellationToken);
            return ServiceResult.Failure("Bu hesap için iki adımlı doğrulama gerekli; bu özellik henüz etkin değil.");
        }

        await LogFailedAsync(email, "Hatalı parola.", cancellationToken);
        return ServiceResult.Failure(InvalidCredentialsMessage);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsAuthenticated)
        {
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.Logout,
                AuditEntityTypes.User,
                _currentUser.UserId,
                _currentUser.UserName), cancellationToken);
        }

        await _signInManager.SignOutAsync();
    }

    private Task LogFailedAsync(string email, string reason, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            AuditActions.LoginFailed,
            AuditEntityTypes.User,
            TargetName: email,
            Details: reason,
            IsSuccess: false,
            UserNameOverride: email), cancellationToken);
}

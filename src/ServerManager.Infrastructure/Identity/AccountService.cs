using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ServerManager.Application;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Account;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Validators.Account;

namespace ServerManager.Infrastructure.Identity;

public class AccountService : IAccountService
{
    private const string InvalidCredentialsMessage = "E-posta veya parola hatalı.";
    private const string LockedOutMessage = "Çok fazla başarısız deneme yapıldı veya hesap kilitlendi. Daha sonra tekrar deneyin.";
    private const string SessionMissingMessage = "Oturum bulunamadı. Lütfen tekrar giriş yapın.";
    private const string WrongPasswordMessage = "Parola hatalı.";
    private const string InactiveMessage = "Hesabınız pasif durumda. Yöneticinizle iletişime geçin.";
    private const string AuthenticatorIssuer = ProductInfo.Name;

    /// <summary>Olmayan kullanıcıda da parola özeti hesaplanır; yanıt süresinden hesabın varlığı anlaşılmaz.</summary>
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<TwoFactorLoginDto> _twoFactorValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly IValidator<EnableAuthenticatorDto> _enableValidator;
    private readonly IValidator<PasswordConfirmationDto> _passwordValidator;
    private readonly IUserSessionRevoker _sessionRevoker;
    private readonly LoginAttemptThrottle _throttle;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<LoginDto> loginValidator,
        IValidator<TwoFactorLoginDto> twoFactorValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IValidator<EnableAuthenticatorDto> enableValidator,
        IValidator<PasswordConfirmationDto> passwordValidator,
        IUserSessionRevoker sessionRevoker,
        LoginAttemptThrottle throttle,
        ILogger<AccountService> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _loginValidator = loginValidator;
        _twoFactorValidator = twoFactorValidator;
        _changePasswordValidator = changePasswordValidator;
        _enableValidator = enableValidator;
        _passwordValidator = passwordValidator;
        _sessionRevoker = sessionRevoker;
        _throttle = throttle;
        _logger = logger;
    }

    public async Task<ServiceResult<SignInStep>> SignInAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _loginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<SignInStep>.ValidationFailure(validation);

        var email = dto.Email.Trim();
        var ipAddress = _currentUser.IpAddress;

        // Bekleme dolmadan gelen denemede parola kontrol edilmez; Identity kilit sayacı da artmaz.
        if (_throttle.GetRetryAfter(email, ipAddress) is { } retryAfter)
        {
            await LogFailedAsync(email, "Deneme sınırı: bekleme süresi dolmadı.", cancellationToken);
            return ServiceResult<SignInStep>.Failure(
                $"Çok fazla başarısız deneme yapıldı. {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))} saniye sonra tekrar deneyin.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Zamanlama farkı olmasın diye gerçek kullanıcıdaki kadar maliyetli bir özet doğrulaması yapılır.
            _userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, dto.Password);
            _throttle.RegisterFailure(email, ipAddress);
            await LogFailedAsync(email, "Kullanıcı bulunamadı.", cancellationToken);
            return ServiceResult<SignInStep>.Failure(
                _throttle.GetAccountFailures(email) >= LoginAttemptThrottle.LockoutThreshold ? LockedOutMessage : InvalidCredentialsMessage);
        }

        if (!user.IsActive)
        {
            // Pasif hesap mesajı yalnızca parolayı bilen kişiye gösterilir; aksi halde hesabın varlığı ortaya çıkar.
            var check = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                await LogFailedAsync(email, "Hesap kilitli (pasif kullanıcı).", cancellationToken);
                return ServiceResult<SignInStep>.Failure(LockedOutMessage);
            }

            if (!check.Succeeded)
            {
                _throttle.RegisterFailure(email, ipAddress);
                await LogFailedAsync(email, "Hatalı parola (pasif kullanıcı).", cancellationToken);
                return ServiceResult<SignInStep>.Failure(InvalidCredentialsMessage);
            }

            await LogFailedAsync(email, "Pasif kullanıcı.", cancellationToken);
            return ServiceResult<SignInStep>.Failure(InactiveMessage);
        }

        var result = await _signInManager.PasswordSignInAsync(user, dto.Password, dto.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _throttle.Reset(email, ipAddress);
            await CompleteSignInAsync(user, user.TwoFactorEnabled ? "Hatırlanan tarayıcı (iki adımlı doğrulama atlandı)" : null, cancellationToken);
            return ServiceResult<SignInStep>.Success(SignInStep.Completed);
        }

        if (result.IsLockedOut)
        {
            await LogFailedAsync(email, "Hesap kilitli.", cancellationToken);
            return ServiceResult<SignInStep>.Failure(LockedOutMessage);
        }

        if (result.RequiresTwoFactor)
        {
            _throttle.Reset(email, ipAddress);
            return ServiceResult<SignInStep>.Success(SignInStep.TwoFactorRequired, "Doğrulama uygulamanızdaki kodu girin.");
        }

        _throttle.RegisterFailure(email, ipAddress);
        await LogFailedAsync(email, "Hatalı parola.", cancellationToken);
        return ServiceResult<SignInStep>.Failure(InvalidCredentialsMessage);
    }

    public async Task<bool> HasPendingTwoFactorAsync() =>
        await _signInManager.GetTwoFactorAuthenticationUserAsync() is not null;

    public async Task<ServiceResult> TwoFactorSignInAsync(TwoFactorLoginDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _twoFactorValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return ServiceResult.Failure("Doğrulama süresi doldu. Lütfen e-posta ve parolanızla tekrar giriş yapın.", ServiceErrorType.Forbidden);

        var email = user.Email ?? string.Empty;
        if (!user.IsActive)
        {
            await LogFailedAsync(email, "Pasif kullanıcı.", cancellationToken);
            return ServiceResult.Failure(InactiveMessage, ServiceErrorType.Forbidden);
        }

        var result = dto.UseRecoveryCode
            ? await _signInManager.TwoFactorRecoveryCodeSignInAsync(dto.Code.Replace(" ", string.Empty).Trim())
            : await _signInManager.TwoFactorAuthenticatorSignInAsync(TwoFactorCodes.Normalize(dto.Code), dto.RememberMe, dto.RememberMachine);

        if (result.Succeeded)
        {
            if (dto.UseRecoveryCode)
            {
                var left = await _userManager.CountRecoveryCodesAsync(user);
                await _auditLogService.LogAsync(new AuditEntry(
                    AuditActions.RecoveryCodeUsed,
                    AuditEntityTypes.User,
                    user.Id.ToString(),
                    email,
                    $"Kalan kurtarma kodu: {left}",
                    UserNameOverride: email), cancellationToken);
            }

            await CompleteSignInAsync(user, dto.UseRecoveryCode ? "Kurtarma kodu" : "İki adımlı doğrulama", cancellationToken);
            return ServiceResult.Success();
        }

        if (result.IsLockedOut)
        {
            await LogFailedAsync(email, "Hesap kilitli (iki adımlı doğrulama).", cancellationToken);
            return ServiceResult.Failure(LockedOutMessage);
        }

        await LogFailedAsync(email, dto.UseRecoveryCode ? "Kurtarma kodu hatalı." : "Doğrulama kodu hatalı.", cancellationToken);
        return ServiceResult.ValidationFailure(nameof(dto.Code), dto.UseRecoveryCode ? "Kurtarma kodu geçersiz veya daha önce kullanılmış." : "Doğrulama kodu hatalı.");
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        if (_currentUser.IsAuthenticated)
        {
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.Logout,
                AuditEntityTypes.User,
                _currentUser.UserId,
                _currentUser.UserName), cancellationToken);
        }

        await _signInManager.SignOutAsync();

        // Çıkış sonrası tarayıcıdaki SignalR bağlantısı açık kalabilir; terminal oturumları burada kapatılır.
        if (!string.IsNullOrEmpty(userId))
            await _sessionRevoker.RevokeAsync(userId, "Oturum kapatıldı; terminal oturumu sonlandırıldı.", cancellationToken);
    }

    public async Task<ServiceResult<AccountSecurityDto>> GetSecurityAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return ServiceResult<AccountSecurityDto>.NotFound(SessionMissingMessage);

        var roles = await _userManager.GetRolesAsync(user);
        return ServiceResult<AccountSecurityDto>.Success(new AccountSecurityDto
        {
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles.ToList(),
            TwoFactorEnabled = user.TwoFactorEnabled,
            RecoveryCodesLeft = user.TwoFactorEnabled ? await _userManager.CountRecoveryCodesAsync(user) : 0,
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user),
            LastLoginAt = user.LastLoginAt
        });
    }

    public async Task<ServiceResult> ChangePasswordAsync(ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _changePasswordValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var user = await GetCurrentUserAsync();
        if (user is null)
            return ServiceResult.NotFound(SessionMissingMessage);

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            var mismatch = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            return mismatch
                ? ServiceResult.ValidationFailure(nameof(dto.CurrentPassword), "Mevcut parola hatalı.")
                : ServiceResult.ValidationFailure(result.Errors.Select(e => new ServiceError(nameof(dto.NewPassword), e.Description)));
        }

        // Parola değişince security stamp yenilenir; diğer oturumlar kapanır, bu oturum yeni stamp ile sürer.
        await _signInManager.RefreshSignInAsync(user);
        await AuditAsync(AuditActions.PasswordChange, user, null, cancellationToken);
        return ServiceResult.Success("Parolanız değiştirildi. Diğer cihazlardaki oturumlar kapatıldı.");
    }

    public async Task<ServiceResult<AuthenticatorSetupDto>> GetAuthenticatorSetupAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return ServiceResult<AuthenticatorSetupDto>.NotFound(SessionMissingMessage);

        if (user.TwoFactorEnabled)
            return ServiceResult<AuthenticatorSetupDto>.Failure("İki adımlı doğrulama zaten açık.", ServiceErrorType.Conflict);

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrEmpty(key))
            return ServiceResult<AuthenticatorSetupDto>.Failure("Doğrulama anahtarı oluşturulamadı.");

        var account = user.Email ?? user.UserName ?? user.Id.ToString();
        return ServiceResult<AuthenticatorSetupDto>.Success(new AuthenticatorSetupDto
        {
            SharedKey = FormatKey(key),
            AuthenticatorUri = BuildAuthenticatorUri(account, key)
        });
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> EnableAuthenticatorAsync(EnableAuthenticatorDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _enableValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<IReadOnlyList<string>>.ValidationFailure(validation);

        var user = await GetCurrentUserAsync();
        if (user is null)
            return ServiceResult<IReadOnlyList<string>>.NotFound(SessionMissingMessage);

        if (user.TwoFactorEnabled)
            return ServiceResult<IReadOnlyList<string>>.Failure("İki adımlı doğrulama zaten açık.", ServiceErrorType.Conflict);

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, TwoFactorCodes.Normalize(dto.Code));
        if (!valid)
            return ServiceResult<IReadOnlyList<string>>.ValidationFailure(nameof(dto.Code), "Kod doğrulanamadı. Telefonunuzun saatinin doğru olduğundan emin olup yeni kodu girin.");

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, TwoFactorCodes.RecoveryCodeCount);
        await _signInManager.RefreshSignInAsync(user);
        await AuditAsync(AuditActions.TwoFactorEnable, user, null, cancellationToken);

        return ServiceResult<IReadOnlyList<string>>.Success((codes ?? []).ToList(), "İki adımlı doğrulama açıldı. Kurtarma kodlarını güvenli bir yere kaydedin.");
    }

    public async Task<ServiceResult> DisableTwoFactorAsync(PasswordConfirmationDto dto, CancellationToken cancellationToken = default)
    {
        var (user, failure) = await ConfirmPasswordAsync(dto, cancellationToken);
        if (failure is not null)
            return failure;

        if (!user!.TwoFactorEnabled)
            return ServiceResult.Failure("İki adımlı doğrulama zaten kapalı.", ServiceErrorType.Conflict);

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _signInManager.ForgetTwoFactorClientAsync();
        await _signInManager.RefreshSignInAsync(user);
        await AuditAsync(AuditActions.TwoFactorDisable, user, null, cancellationToken);
        return ServiceResult.Success("İki adımlı doğrulama kapatıldı.");
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> RegenerateRecoveryCodesAsync(PasswordConfirmationDto dto, CancellationToken cancellationToken = default)
    {
        var (user, failure) = await ConfirmPasswordAsync(dto, cancellationToken);
        if (failure is not null)
        {
            return failure.ErrorType == ServiceErrorType.Validation
                ? ServiceResult<IReadOnlyList<string>>.ValidationFailure(failure.Errors)
                : ServiceResult<IReadOnlyList<string>>.Failure(failure.Message ?? WrongPasswordMessage, failure.ErrorType);
        }

        if (!user!.TwoFactorEnabled)
            return ServiceResult<IReadOnlyList<string>>.Failure("Önce iki adımlı doğrulamayı açın.", ServiceErrorType.Conflict);

        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, TwoFactorCodes.RecoveryCodeCount);
        await AuditAsync(AuditActions.RecoveryCodesRegenerate, user, null, cancellationToken);
        return ServiceResult<IReadOnlyList<string>>.Success((codes ?? []).ToList(), "Yeni kurtarma kodları oluşturuldu; eski kodlar artık geçersiz.");
    }

    public async Task<ServiceResult> ForgetMachineAsync(CancellationToken cancellationToken = default)
    {
        await _signInManager.ForgetTwoFactorClientAsync();
        return ServiceResult.Success("Bu tarayıcı unutuldu; sonraki girişte doğrulama kodu sorulacak.");
    }

    private async Task<(ApplicationUser? User, ServiceResult? Failure)> ConfirmPasswordAsync(PasswordConfirmationDto dto, CancellationToken cancellationToken)
    {
        var validation = await _passwordValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return (null, ServiceResult.ValidationFailure(validation));

        var user = await GetCurrentUserAsync();
        if (user is null)
            return (null, ServiceResult.NotFound(SessionMissingMessage));

        var check = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return (null, ServiceResult.Failure(LockedOutMessage, ServiceErrorType.Forbidden));
        if (!check.Succeeded)
            return (null, ServiceResult.ValidationFailure(nameof(dto.Password), WrongPasswordMessage));

        return (user, null);
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync() =>
        _signInManager.Context.User.Identity?.IsAuthenticated == true
            ? await _userManager.GetUserAsync(_signInManager.Context.User)
            : null;

    private async Task CompleteSignInAsync(ApplicationUser user, string? method, CancellationToken cancellationToken)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Kullanıcı giriş yaptı: {UserId}", user.Id);
        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.Login,
            AuditEntityTypes.User,
            user.Id.ToString(),
            user.Email,
            method,
            UserNameOverride: user.Email), cancellationToken);
    }

    private Task AuditAsync(string action, ApplicationUser user, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.User,
            user.Id.ToString(),
            user.Email,
            details), cancellationToken);

    private Task LogFailedAsync(string email, string reason, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            AuditActions.LoginFailed,
            AuditEntityTypes.User,
            TargetName: email,
            Details: reason,
            IsSuccess: false,
            UserNameOverride: email), cancellationToken);

    private static string FormatKey(string key)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return builder.ToString().ToLowerInvariant();
    }

    internal static string BuildAuthenticatorUri(string account, string key)
    {
        var issuer = Uri.EscapeDataString(AuthenticatorIssuer);
        return $"otpauth://totp/{issuer}:{Uri.EscapeDataString(account)}?secret={key}&issuer={issuer}&digits=6";
    }
}

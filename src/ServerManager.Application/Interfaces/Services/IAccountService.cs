using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Account;

namespace ServerManager.Application.Interfaces.Services;

public interface IAccountService
{
    /// <summary>Parola doğruysa ve hesapta iki adımlı doğrulama açıksa <see cref="SignInStep.TwoFactorRequired"/> döner.</summary>
    Task<ServiceResult<SignInStep>> SignInAsync(LoginDto dto, CancellationToken cancellationToken = default);

    /// <summary>Parolası doğrulanmış ve ikinci adımı bekleyen bir giriş var mı.</summary>
    Task<bool> HasPendingTwoFactorAsync();

    Task<ServiceResult> TwoFactorSignInAsync(TwoFactorLoginDto dto, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<AccountSecurityDto>> GetSecurityAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult> ChangePasswordAsync(ChangePasswordDto dto, CancellationToken cancellationToken = default);

    /// <summary>Kurulum anahtarı yoksa oluşturur; iki adımlı doğrulama zaten açıksa hata döner.</summary>
    Task<ServiceResult<AuthenticatorSetupDto>> GetAuthenticatorSetupAsync(CancellationToken cancellationToken = default);

    /// <summary>Kod doğrulanınca iki adımlı doğrulamayı açar ve kurtarma kodlarını döner (yalnızca bir kez gösterilir).</summary>
    Task<ServiceResult<IReadOnlyList<string>>> EnableAuthenticatorAsync(EnableAuthenticatorDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DisableTwoFactorAsync(PasswordConfirmationDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<string>>> RegenerateRecoveryCodesAsync(PasswordConfirmationDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> ForgetMachineAsync(CancellationToken cancellationToken = default);
}

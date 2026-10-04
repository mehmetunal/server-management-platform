using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.Account;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.Options;
using ServerManager.Web.RateLimiting;
using ServerManager.Web.Services;

namespace ServerManager.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly TwoFactorOptions _twoFactorOptions;

    public AccountController(IAccountService accountService, IOptions<TwoFactorOptions> twoFactorOptions)
    {
        _accountService = accountService;
        _twoFactorOptions = twoFactorOptions.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View(new LoginDto { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _accountService.SignInAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Giriş yapılamadı.");

        if (result.Data == SignInStep.TwoFactorRequired)
        {
            return this.ApiSuccess(result.Message, Url.Action(nameof(LoginWith2fa), new
            {
                returnUrl = SafeReturnUrl(dto.ReturnUrl),
                rememberMe = dto.RememberMe
            }));
        }

        return this.ApiSuccess(result.Message, SafeReturnUrl(dto.ReturnUrl) ?? Url.Action("Index", "Dashboard"));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWith2fa(string? returnUrl = null, bool rememberMe = false)
    {
        if (!await _accountService.HasPendingTwoFactorAsync())
            return RedirectToAction(nameof(Login), new { returnUrl = SafeReturnUrl(returnUrl) });

        return View(new TwoFactorLoginDto { ReturnUrl = SafeReturnUrl(returnUrl), RememberMe = rememberMe });
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> LoginWith2fa(TwoFactorLoginDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _accountService.TwoFactorSignInAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Doğrulama yapılamadı.");

        return this.ApiSuccess(result.Message, SafeReturnUrl(dto.ReturnUrl) ?? Url.Action("Index", "Dashboard"));
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _accountService.SignOutAsync(cancellationToken);
        return this.ApiSuccess("Oturum kapatıldı.", Url.Action(nameof(Login)));
    }

    [HttpGet]
    public async Task<IActionResult> Security(CancellationToken cancellationToken)
    {
        var result = await _accountService.GetSecurityAsync(cancellationToken);
        if (!result.IsSuccess)
            return RedirectToAction(nameof(Login));

        return View(new AccountSecurityViewModel { Security = result.Data!, TwoFactorRequired = _twoFactorOptions.Required });
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _accountService.ChangePasswordAsync(dto, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Parola değiştirilemedi.");
    }

    [HttpPost]
    public async Task<IActionResult> AuthenticatorSetup(CancellationToken cancellationToken)
    {
        var result = await _accountService.GetAuthenticatorSetupAsync(cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kurulum başlatılamadı.");

        return Ok(ApiResponse<AuthenticatorSetupResponse>.Success(new AuthenticatorSetupResponse
        {
            SharedKey = result.Data!.SharedKey,
            QrCodeDataUri = QrCodeImage.ToPngDataUri(result.Data.AuthenticatorUri)
        }));
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _accountService.EnableAuthenticatorAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "İki adımlı doğrulama açılamadı.");

        return Ok(ApiResponse<IReadOnlyList<string>>.Success(result.Data, result.Message ?? string.Empty));
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> DisableTwoFactor(PasswordConfirmationDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _accountService.DisableTwoFactorAsync(dto, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İki adımlı doğrulama kapatılamadı.");
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> RegenerateRecoveryCodes(PasswordConfirmationDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _accountService.RegenerateRecoveryCodesAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kurtarma kodları yenilenemedi.");

        return Ok(ApiResponse<IReadOnlyList<string>>.Success(result.Data, result.Message ?? string.Empty));
    }

    [HttpPost]
    public async Task<IActionResult> ForgetMachine(CancellationToken cancellationToken)
    {
        var result = await _accountService.ForgetMachineAsync(cancellationToken);
        return this.ApiSuccess(result.Message);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}

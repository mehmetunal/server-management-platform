using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.DTOs.Account;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Extensions;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
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
        var result = await _accountService.SignInAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            dto.Password = string.Empty;
            return View(dto);
        }

        if (!string.IsNullOrEmpty(dto.ReturnUrl) && Url.IsLocalUrl(dto.ReturnUrl))
            return LocalRedirect(dto.ReturnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _accountService.SignOutAsync(cancellationToken);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

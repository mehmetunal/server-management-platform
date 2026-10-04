using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>Güvenlik merkezi: tüm sunucuların son tarama özeti ve elle tarama.</summary>
[HasPermission(Permissions.SecurityView)]
public class SecurityController : Controller
{
    private readonly ISecurityService _securityService;

    public SecurityController(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _securityService.GetOverviewAsync(cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.SecurityScan)]
    [EnableRateLimiting(RateLimitPolicies.SecurityScan)]
    public async Task<IActionResult> Scan(Guid id, CancellationToken cancellationToken)
    {
        var result = await _securityService.ScanAsync(id, SecurityScanTrigger.Manual, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Güvenlik taraması yapılamadı.");

        return this.ApiSuccess(result.Message);
    }
}

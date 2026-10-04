using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Security sekmesi: son tarama raporu ve geçmiş.</summary>
[HasPermission(Permissions.SecurityView)]
public class ServerSecurityController : Controller
{
    private readonly ISecurityService _securityService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerSecurityController(ISecurityService securityService, ServerPageBuilder pageBuilder)
    {
        _securityService = securityService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.SecurityTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        var report = await _securityService.GetServerReportAsync(id, cancellationToken);
        if (!report.IsSuccess || report.Data is null)
            return NotFound();

        return View(new ServerSecurityViewModel { Page = page, Report = report.Data });
    }
}

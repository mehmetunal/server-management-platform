using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Authorization;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.DashboardView)]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return View(summary);
    }
}

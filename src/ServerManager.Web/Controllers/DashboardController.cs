using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Models;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.DashboardView)]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IMonitoringService _monitoringService;

    public DashboardController(IDashboardService dashboardService, IMonitoringService monitoringService)
    {
        _dashboardService = dashboardService;
        _monitoringService = monitoringService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return View(summary);
    }

    [HttpGet]
    public async Task<IActionResult> Live(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<DashboardLiveModel>.Success(DashboardLiveModel.From(summary)));
    }

    [HttpGet]
    public async Task<IActionResult> FleetSeries(string? range, CancellationToken cancellationToken)
    {
        var metricRange = MetricRanges.Parse(range);
        var points = await _monitoringService.GetFleetSeriesAsync(metricRange, cancellationToken);
        return Ok(ApiResponse<MetricSeriesModel>.Success(MetricSeriesModel.From(metricRange, points)));
    }
}

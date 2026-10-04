using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Alerts sekmesi: yalnızca bu sunucuya ait alarmlar.</summary>
[HasPermission(Permissions.AlertView)]
public class ServerAlertsController : Controller
{
    private readonly IAlertService _alertService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerAlertsController(IAlertService alertService, ServerPageBuilder pageBuilder)
    {
        _alertService = alertService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, [FromQuery] AlertEventFilterDto filter, CancellationToken cancellationToken)
    {
        filter.ServerId = id;
        var list = new AlertIndexViewModel { Events = await _alertService.SearchAsync(filter, cancellationToken), Filter = filter };
        if (Request.IsAjax())
            return PartialView("~/Views/Alerts/_AlertList.cshtml", list);

        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.AlertsTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new ServerAlertsViewModel { Page = page, List = list });
    }
}

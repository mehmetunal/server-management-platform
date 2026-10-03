using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AlertView)]
public class AlertsController : Controller
{
    private readonly IAlertService _alertService;
    private readonly IAlertRuleService _ruleService;

    public AlertsController(IAlertService alertService, IAlertRuleService ruleService)
    {
        _alertService = alertService;
        _ruleService = ruleService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AlertEventFilterDto filter, CancellationToken cancellationToken)
    {
        var events = await _alertService.SearchAsync(filter, cancellationToken);
        if (Request.IsAjax())
            return PartialView("_AlertList", new AlertIndexViewModel { Events = events, Filter = filter });

        return View(new AlertIndexViewModel
        {
            Events = events,
            Filter = filter,
            Summary = await _alertService.GetSummaryAsync(cancellationToken),
            Servers = await _ruleService.GetServerOptionsAsync(cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var summary = await _alertService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<AlertSummaryDto>.Success(summary));
    }

    [HttpPost]
    [HasPermission(Permissions.AlertAcknowledge)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        var result = await _alertService.AcknowledgeAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Alarm üstlenilemedi.");

        return this.ApiSuccess(result.Message);
    }
}

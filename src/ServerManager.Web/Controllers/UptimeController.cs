using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AlertView)]
public class UptimeController : Controller
{
    public const string ServerOptionsKey = "ServerOptions";

    private readonly IUptimeService _uptimeService;
    private readonly IAlertRuleService _ruleService;

    public UptimeController(IUptimeService uptimeService, IAlertRuleService ruleService)
    {
        _uptimeService = uptimeService;
        _ruleService = ruleService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UptimeFilterDto filter, CancellationToken cancellationToken)
    {
        var checks = await _uptimeService.SearchAsync(filter, cancellationToken);
        var model = new UptimeIndexViewModel
        {
            Checks = checks,
            Filter = filter,
            Servers = Request.IsAjax() ? [] : await _ruleService.GetServerOptionsAsync(cancellationToken)
        };
        return Request.IsAjax() ? PartialView("_UptimeList", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _uptimeService.GetDetailsAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        return Request.IsAjax() ? PartialView("_UptimeDetails", result.Data) : View(result.Data);
    }

    [HttpGet]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData[ServerOptionsKey] = await _ruleService.GetServerOptionsAsync(cancellationToken);
        return View(new UptimeCheckFormDto());
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Create(UptimeCheckFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _uptimeService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Uptime kontrolü eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id = result.Data }));
    }

    [HttpGet]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _uptimeService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        ViewData[ServerOptionsKey] = await _ruleService.GetServerOptionsAsync(cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Edit(Guid id, UptimeCheckFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _uptimeService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Uptime kontrolü güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id }));
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _uptimeService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Uptime kontrolü silinemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    [EnableRateLimiting(RateLimitPolicies.AlertingAction)]
    public async Task<IActionResult> CheckNow(Guid id, CancellationToken cancellationToken)
    {
        var result = await _uptimeService.CheckNowAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kontrol çalıştırılamadı.");

        return Ok(ApiResponse<UptimeProbeResult>.Success(result.Data, result.Message ?? string.Empty));
    }
}

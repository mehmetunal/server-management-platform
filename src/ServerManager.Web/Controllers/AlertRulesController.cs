using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Alerting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AlertView)]
public class AlertRulesController : Controller
{
    public const string ServerOptionsKey = "ServerOptions";
    public const string ChannelOptionsKey = "ChannelOptions";
    public const string ServiceOptionsKey = "ServiceOptions";

    private readonly IAlertRuleService _ruleService;
    private readonly INotificationChannelService _channelService;

    public AlertRulesController(IAlertRuleService ruleService, INotificationChannelService channelService)
    {
        _ruleService = ruleService;
        _channelService = channelService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var rules = await _ruleService.GetRulesAsync(cancellationToken);
        return Request.IsAjax() ? PartialView("_RuleList", rules) : View(rules);
    }

    [HttpGet]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Create(AlertRuleKind? kind, Guid? serviceId, CancellationToken cancellationToken)
    {
        await SetFormDataAsync(cancellationToken);
        var form = new AlertRuleFormDto();
        if (kind is { } selected && Enum.IsDefined(selected))
        {
            var (threshold, duration) = AlertRuleKinds.Defaults(selected);
            form.Kind = selected;
            form.Threshold = threshold;
            form.DurationMinutes = duration;
            form.Name = AlertRuleKinds.DisplayName(selected);
            if (AlertRuleKinds.UsesService(selected))
                form.ManagedServiceId = serviceId;
        }

        return View(form);
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Create(AlertRuleFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _ruleService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kural eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _ruleService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        await SetFormDataAsync(cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Edit(Guid id, AlertRuleFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _ruleService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kural güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Toggle(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var result = await _ruleService.SetEnabledAsync(id, enabled, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kural güncellenemedi.");

        return this.ApiSuccess(result.Message);
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _ruleService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Kural silinemedi.");

        return this.ApiSuccess(result.Message);
    }

    private async Task SetFormDataAsync(CancellationToken cancellationToken)
    {
        ViewData[ServerOptionsKey] = await _ruleService.GetServerOptionsAsync(cancellationToken);
        ViewData[ChannelOptionsKey] = await _channelService.GetChannelOptionsAsync(cancellationToken);
        ViewData[ServiceOptionsKey] = await _ruleService.GetServiceOptionsAsync(cancellationToken);
    }
}

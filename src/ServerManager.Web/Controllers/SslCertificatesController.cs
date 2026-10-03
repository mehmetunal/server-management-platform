using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.AlertView)]
public class SslCertificatesController : Controller
{
    public const string ServerOptionsKey = "ServerOptions";

    private readonly ISslCertificateService _sslService;
    private readonly IAlertRuleService _ruleService;

    public SslCertificatesController(ISslCertificateService sslService, IAlertRuleService ruleService)
    {
        _sslService = sslService;
        _ruleService = ruleService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SslMonitorFilterDto filter, CancellationToken cancellationToken)
    {
        var model = new SslIndexViewModel { Monitors = await _sslService.SearchAsync(filter, cancellationToken), Filter = filter };
        return Request.IsAjax() ? PartialView("_SslList", model) : View(model);
    }

    [HttpGet]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData[ServerOptionsKey] = await _ruleService.GetServerOptionsAsync(cancellationToken);
        return View(new SslMonitorFormDto());
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    [EnableRateLimiting(RateLimitPolicies.AlertingAction)]
    public async Task<IActionResult> Create(SslMonitorFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _sslService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "SSL izleme eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sslService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        ViewData[ServerOptionsKey] = await _ruleService.GetServerOptionsAsync(cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    [EnableRateLimiting(RateLimitPolicies.AlertingAction)]
    public async Task<IActionResult> Edit(Guid id, SslMonitorFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _sslService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "SSL izleme güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sslService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "SSL izleme silinemedi.");

        return this.ApiSuccess(result.Message);
    }

    [HttpPost]
    [HasPermission(Permissions.AlertManage)]
    [EnableRateLimiting(RateLimitPolicies.AlertingAction)]
    public async Task<IActionResult> CheckNow(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sslService.CheckNowAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Sertifika kontrol edilemedi.");

        return Ok(ApiResponse<SslMonitorListItemDto>.Success(result.Data, result.Message ?? string.Empty));
    }
}

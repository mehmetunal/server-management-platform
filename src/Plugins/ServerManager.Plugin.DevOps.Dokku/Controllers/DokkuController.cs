using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Common;
using ServerManager.Application.Monitoring;
using ServerManager.Plugin.DevOps.Dokku.DTOs;
using ServerManager.Plugin.DevOps.Dokku.Models;
using ServerManager.Plugin.DevOps.Dokku.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Plugin.DevOps.Dokku.Controllers;

[HasPermission(DokkuPermissions.View)]
public class DokkuController : Controller
{
    private const string PanelErrorView = "_PanelError";

    private readonly IDokkuService _dokkuService;
    private readonly ServerPageBuilder _pageBuilder;

    public DokkuController(IDokkuService dokkuService, ServerPageBuilder pageBuilder)
    {
        _dokkuService = dokkuService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, DokkuPlugin.ServerTabKey, MetricRange.OneHour, cancellationToken);
        return page is null ? NotFound() : View(new DokkuPageViewModel { Page = page });
    }

    [HttpGet]
    public async Task<IActionResult> StatusPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dokkuService.GetOverviewAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            Response.StatusCode = result.ErrorType == ServiceErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
            return PartialView(PanelErrorView, result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "Dokku bilgisi alınamadı.");
        }

        return PartialView("_StatusPanel", new DokkuPanelModel<DokkuOverviewDto> { ServerId = id, Data = result.Data! });
    }

    [HttpPost]
    [HasPermission(DokkuPermissions.Manage)]
    [EnableRateLimiting(DokkuPlugin.RateLimitPolicy)]
    public async Task<IActionResult> StartInstall(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dokkuService.StartInstallAsync(id, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "Kurulum başlatılamadı.");
    }

    [HttpPost]
    [HasPermission(DokkuPermissions.Manage)]
    [EnableRateLimiting(DokkuPlugin.RateLimitPolicy)]
    public async Task<IActionResult> Restart(Guid id, string app, CancellationToken cancellationToken)
    {
        var result = await _dokkuService.RestartAsync(id, app, cancellationToken);
        return result.IsSuccess
            ? this.ApiSuccess(result.Message)
            : this.ApiFailure(result, "Uygulama yeniden başlatılamadı.");
    }
}

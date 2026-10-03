using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Authorization;
using ServerManager.Web.Dokploy;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;
using ServerManager.Web.Services;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.DokployView)]
public class DokployController : Controller
{
    private const string PanelErrorView = "_PanelError";

    private readonly IDokployService _dokployService;
    private readonly DokployInstallationManager _installationManager;
    private readonly ICurrentUserService _currentUser;
    private readonly ServerPageBuilder _pageBuilder;

    public DokployController(
        IDokployService dokployService,
        DokployInstallationManager installationManager,
        ICurrentUserService currentUser,
        ServerPageBuilder pageBuilder)
    {
        _dokployService = dokployService;
        _installationManager = installationManager;
        _currentUser = currentUser;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.DokployTab, MetricRange.OneHour, cancellationToken);
        return page is null ? NotFound() : View(new DokployPageViewModel { Page = page });
    }

    [HttpGet]
    public async Task<IActionResult> Install(Guid id, Guid? installationId, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.DokployTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        installationId ??= await _dokployService.GetRunningInstallationIdAsync(id, cancellationToken);
        return View(new DokployPageViewModel { Page = page, InstallationId = installationId });
    }

    [HttpGet]
    public async Task<IActionResult> StatusPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dokployService.GetOverviewAsync(id, cancellationToken);
        return PanelResult(id, result, "_StatusPanel");
    }

    [HttpGet]
    public async Task<IActionResult> ProjectsPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dokployService.GetProjectsAsync(id, cancellationToken);
        return PanelResult(id, result, "_ProjectsPanel");
    }

    [HttpGet]
    [HasPermission(Permissions.DokployInstall)]
    [EnableRateLimiting(RateLimitPolicies.DokployAction)]
    public async Task<IActionResult> CompatibilityPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dokployService.CheckCompatibilityAsync(id, cancellationToken);
        return PanelResult(id, result, "_CompatibilityPanel");
    }

    [HttpPost]
    [HasPermission(Permissions.DokployInstall)]
    [EnableRateLimiting(RateLimitPolicies.DokployAction)]
    public async Task<IActionResult> StartInstall(Guid id, DokployInstallRequestDto dto, CancellationToken cancellationToken)
    {
        var actor = new DokployActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var result = await _installationManager.StartAsync(id, dto, actor, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<Guid>.Success(result.Data, result.Message ?? "Kurulum başlatıldı."))
            : this.ApiFailure(result, "Kurulum başlatılamadı.");
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.DokployAction)]
    public async Task<IActionResult> HealthCheck(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dokployService.RunHealthCheckAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<DokployHealthResultDto>.Success(result.Data, result.Data!.Message))
            : this.ApiFailure(result, "Sağlık kontrolü yapılamadı.");
    }

    [HttpPost]
    [HasPermission(Permissions.DokployManage)]
    [EnableRateLimiting(RateLimitPolicies.DokployAction)]
    public async Task<IActionResult> Settings(Guid id, DokploySettingsDto dto, CancellationToken cancellationToken) =>
        ActionResultFor(await _dokployService.SaveSettingsAsync(id, dto, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.DokployManage)]
    public async Task<IActionResult> RemoveApiKey(Guid id, CancellationToken cancellationToken) =>
        ActionResultFor(await _dokployService.RemoveApiKeyAsync(id, cancellationToken));

    private IActionResult PanelResult<T>(Guid serverId, ServiceResult<T> result, string viewName)
    {
        if (!result.IsSuccess)
        {
            Response.StatusCode = result.ErrorType == ServiceErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
            return PartialView(PanelErrorView, result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "Dokploy bilgisi alınamadı.");
        }

        return PartialView(viewName, new DokployPanelModel<T> { ServerId = serverId, Data = result.Data! });
    }

    private IActionResult ActionResultFor(ServiceResult result) =>
        result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İşlem başarısız.");
}

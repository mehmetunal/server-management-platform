using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Application.ServerSystem;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Services, Processes, Logs, Network ve Storage sekmeleri (SSH ile canlı okunur).</summary>
[HasPermission(Permissions.SystemView)]
public class ServerSystemController : Controller
{
    private const string ErrorView = "_SystemError";

    private readonly IServerSystemService _systemService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerSystemController(IServerSystemService systemService, ServerPageBuilder pageBuilder)
    {
        _systemService = systemService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public Task<IActionResult> Services(Guid id, CancellationToken cancellationToken) =>
        PageAsync(id, ServerPageViewModel.ServicesTab, nameof(ServicesPanel), "Servisler sunucudan okunuyor…", null, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> ServicesPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _systemService.GetServicesAsync(id, cancellationToken);
        return result.IsSuccess
            ? PartialView("_Services", new ServerServicesPanelModel { ServerId = id, Services = result.Data! })
            : Error(result);
    }

    [HttpPost]
    [HasPermission(Permissions.SystemManage)]
    [EnableRateLimiting(RateLimitPolicies.SystemAction)]
    public async Task<IActionResult> ControlService(Guid id, ServiceManagerKind manager, string name, ServiceAction action, CancellationToken cancellationToken)
    {
        var result = await _systemService.ControlServiceAsync(id, manager, name, action, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Servis işlemi yapılamadı.");
    }

    [HttpGet]
    public Task<IActionResult> Processes(Guid id, CancellationToken cancellationToken) =>
        PageAsync(id, ServerPageViewModel.ProcessesTab, nameof(ProcessesPanel), "Process listesi sunucudan okunuyor…", null, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> ProcessesPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _systemService.GetProcessesAsync(id, cancellationToken);
        return result.IsSuccess
            ? PartialView("_Processes", new ServerProcessesPanelModel { ServerId = id, Processes = result.Data! })
            : Error(result);
    }

    [HttpPost]
    [HasPermission(Permissions.SystemManage)]
    [EnableRateLimiting(RateLimitPolicies.SystemAction)]
    public async Task<IActionResult> SignalProcess(Guid id, int pid, ProcessSignal signal, bool kill, string? name, CancellationToken cancellationToken)
    {
        var result = await _systemService.SignalProcessAsync(id, pid, kill ? ProcessSignal.Kill : signal, name, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Process sonlandırılamadı.");
    }

    [HttpGet]
    public Task<IActionResult> Logs(Guid id, [FromQuery] LogRequest request, CancellationToken cancellationToken) =>
        PageAsync(id, ServerPageViewModel.LogsTab, nameof(LogsPanel), "Loglar sunucudan okunuyor…", request, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> LogsPanel(Guid id, [FromQuery] LogRequest request, CancellationToken cancellationToken)
    {
        request.Lines = ServerSystemRules.NormalizeLines(request.Lines);
        var result = await _systemService.GetLogsAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? PartialView("_Logs", new ServerLogsPanelModel { Request = request, Snapshot = result.Data! })
            : Error(result);
    }

    [HttpGet]
    public Task<IActionResult> Network(Guid id, CancellationToken cancellationToken) =>
        PageAsync(id, ServerPageViewModel.NetworkTab, nameof(NetworkPanel), "Ağ bilgileri sunucudan okunuyor…", null, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> NetworkPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _systemService.GetNetworkAsync(id, cancellationToken);
        return result.IsSuccess ? PartialView("_Network", result.Data!) : Error(result);
    }

    [HttpGet]
    public Task<IActionResult> Storage(Guid id, CancellationToken cancellationToken) =>
        PageAsync(id, ServerPageViewModel.StorageTab, nameof(StoragePanel), "Disk bilgileri sunucudan okunuyor…", null, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> StoragePanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _systemService.GetStorageAsync(id, cancellationToken);
        return result.IsSuccess ? PartialView("_Storage", result.Data!) : Error(result);
    }

    private async Task<IActionResult> PageAsync(
        Guid id, string tab, string panelAction, string loadingText, LogRequest? logRequest, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, tab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        if (logRequest is not null)
            logRequest.Lines = ServerSystemRules.NormalizeLines(logRequest.Lines);

        var panelUrl = logRequest is null
            ? Url.Action(panelAction, new { id })!
            : Url.Action(panelAction, new
            {
                id,
                source = logRequest.Source,
                unit = logRequest.Unit,
                path = logRequest.Path,
                priority = logRequest.Priority,
                lines = logRequest.Lines,
                filter = logRequest.Filter
            })!;

        return View("Section", new ServerSystemPageViewModel
        {
            Page = page,
            PanelUrl = panelUrl,
            LoadingText = loadingText,
            LogRequest = logRequest
        });
    }

    private PartialViewResult Error(ServiceResult result)
    {
        Response.StatusCode = result.ErrorType == ServiceErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
        return PartialView(ErrorView, result.Message ?? "Sunucudan bilgi alınamadı.");
    }
}

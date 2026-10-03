using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Authorization;
using ServerManager.Web.Extensions;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.ServerView)]
public class ServersController : Controller
{
    private static readonly string[] SecretFieldNames =
    [
        nameof(ServerFormDto.Password),
        nameof(ServerFormDto.PrivateKey),
        nameof(ServerFormDto.Passphrase),
        nameof(ServerFormDto.SudoPassword)
    ];

    private readonly IServerService _serverService;
    private readonly IMonitoringService _monitoringService;
    private readonly MonitoringOptions _monitoringOptions;

    public ServersController(IServerService serverService, IMonitoringService monitoringService, IOptions<MonitoringOptions> monitoringOptions)
    {
        _serverService = serverService;
        _monitoringService = monitoringService;
        _monitoringOptions = monitoringOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ServerFilterDto filter, CancellationToken cancellationToken)
    {
        var servers = await _serverService.SearchAsync(filter, cancellationToken);
        var tags = await _serverService.GetTagNamesAsync(cancellationToken);
        var resources = await _monitoringService.GetLatestSummariesAsync(servers.Items.Select(s => s.Id).ToList(), cancellationToken);

        return View(new ServerIndexViewModel
        {
            Servers = servers,
            Filter = filter,
            Tags = tags,
            Resources = resources,
            Thresholds = _monitoringOptions
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await BuildPageAsync(id, ServerPageViewModel.OverviewTab, MetricRange.OneHour, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Metrics(Guid id, string? range, CancellationToken cancellationToken)
    {
        var model = await BuildPageAsync(id, ServerPageViewModel.MetricsTab, MetricRanges.Parse(range), cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> MetricSeries(Guid id, string? range, CancellationToken cancellationToken)
    {
        var metricRange = MetricRanges.Parse(range);
        var result = await _monitoringService.GetSeriesAsync(id, metricRange, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<MetricSeriesModel>.Fail(result.Message ?? "Sunucu bulunamadı.", StatusCodes.Status404NotFound));

        return Ok(ApiResponse<MetricSeriesModel>.Success(MetricSeriesModel.From(metricRange, result.Data!)));
    }

    [HttpGet]
    public async Task<IActionResult> MetricsPanel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _monitoringService.GetOverviewAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        return PartialView("_MetricsPanel", new MetricsPanelViewModel { Monitoring = result.Data!, Thresholds = _monitoringOptions });
    }

    [HttpPost]
    [HasPermission(Permissions.ServerConnect)]
    [EnableRateLimiting(RateLimitPolicies.MetricsCollect)]
    public async Task<IActionResult> CollectMetrics(Guid id, CancellationToken cancellationToken)
    {
        var result = await _monitoringService.CollectAsync(id, manual: true, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound(ApiResponse<MonitoringUpdatePayload>.Fail(result.Message ?? "Sunucu bulunamadı.", StatusCodes.Status404NotFound));

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<MonitoringUpdatePayload>.Fail(result.Message ?? "Metrikler toplanamadı.", StatusCodes.Status400BadRequest));

        var payload = MonitoringUpdatePayload.From(result.Data!);
        return Ok(ApiResponse<MonitoringUpdatePayload>.Success(payload, payload.Message));
    }

    [HttpGet]
    [HasPermission(Permissions.ServerCreate)]
    public IActionResult Create() => View(new CreateServerDto());

    [HttpPost]
    [HasPermission(Permissions.ServerCreate)]
    public async Task<IActionResult> Create(CreateServerDto dto, CancellationToken cancellationToken)
    {
        var result = await _serverService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            ClearSecrets(dto);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpGet]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.GetForEditAsync(id, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.ServerEdit)]
    public async Task<IActionResult> Edit(Guid id, UpdateServerDto dto, CancellationToken cancellationToken)
    {
        dto.Id = id;
        var result = await _serverService.UpdateAsync(dto, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (!result.IsSuccess)
        {
            ModelState.AddServiceErrors(result);
            ClearSecrets(dto);
            await RestoreSecretFlagsAsync(dto, cancellationToken);
            return View(dto);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [HasPermission(Permissions.ServerDelete)]
    public async Task<IActionResult> Delete(Guid id, string? confirmationName, CancellationToken cancellationToken)
    {
        var result = await _serverService.DeleteAsync(id, confirmationName, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Message ?? result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [HasPermission(Permissions.ServerConnect)]
    [EnableRateLimiting(RateLimitPolicies.ConnectionTest)]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.TestConnectionAsync(id, cancellationToken);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound(ApiResponse<ConnectionTestResultDto>.Fail(result.Message ?? "Sunucu bulunamadı.", StatusCodes.Status404NotFound));

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<ConnectionTestResultDto>.Fail(result.Message ?? "Bağlantı testi yapılamadı.", StatusCodes.Status400BadRequest));

        return Ok(ApiResponse<ConnectionTestResultDto>.Success(result.Data, result.Data!.Message));
    }

    private async Task<ServerPageViewModel?> BuildPageAsync(Guid id, string activeTab, MetricRange range, CancellationToken cancellationToken)
    {
        var details = await _serverService.GetDetailsAsync(id, cancellationToken);
        if (!details.IsSuccess)
            return null;

        var monitoring = await _monitoringService.GetOverviewAsync(id, cancellationToken);
        if (!monitoring.IsSuccess)
            return null;

        return new ServerPageViewModel
        {
            Server = details.Data!,
            Monitoring = monitoring.Data!,
            Thresholds = _monitoringOptions,
            ActiveTab = activeTab,
            Range = range
        };
    }

    private void ClearSecrets(ServerFormDto dto)
    {
        dto.ClearSecrets();
        foreach (var name in SecretFieldNames)
        {
            if (ModelState.TryGetValue(name, out var entry))
            {
                entry.RawValue = null;
                entry.AttemptedValue = null;
            }
        }
    }

    private async Task RestoreSecretFlagsAsync(UpdateServerDto dto, CancellationToken cancellationToken)
    {
        var current = await _serverService.GetForEditAsync(dto.Id, cancellationToken);
        if (current.Data is null)
            return;

        dto.HasPassword = current.Data.HasPassword;
        dto.HasPrivateKey = current.Data.HasPrivateKey;
        dto.HasPassphrase = current.Data.HasPassphrase;
        dto.HasSudoPassword = current.Data.HasSudoPassword;
    }
}

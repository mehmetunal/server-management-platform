using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>
/// Sunucu "Kaynak Kullanımı" sekmesi: yük, bellek, CPU dağılımı, en çok kaynak kullanan process ve container'lar, disk doluluğu,
/// en büyük klasör / dosyalar ve "Neden yavaş?" bulguları. Tamamen salt okunurdur; process sonlandırma ServerSystem'deki
/// mevcut uç noktayla (system.manage) yapılır.
/// </summary>
[HasPermission(Permissions.SystemView)]
public class ServerResourcesController : Controller
{
    private const string ErrorView = "~/Views/ServerSystem/_SystemError.cshtml";

    private readonly IResourceUsageService _resourceService;
    private readonly IResourceHistoryService _historyService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerResourcesController(IResourceUsageService resourceService, IResourceHistoryService historyService, ServerPageBuilder pageBuilder)
    {
        _resourceService = resourceService;
        _historyService = historyService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.ResourcesTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new ServerResourcesPageViewModel
        {
            Page = page,
            PanelUrl = Url.Action(nameof(Overview), new { id })!,
            DiskUrl = Url.Action(nameof(Disk), new { id })!,
            HistoryUrl = Url.Action(nameof(History), new { id })!,
            SnapshotUrl = Url.Action(nameof(ProcessSnapshot), new { id })!,
            HistoryEnabled = _historyService.Enabled,
            HistoryIntervalMinutes = _historyService.IntervalMinutes
        });
    }

    /// <summary>
    /// Kaynak geçmişi: hazır aralık (<c>1h</c>, <c>6h</c>, <c>24h</c>, <c>7d</c>) veya özel aralık (<paramref name="from"/> /
    /// <paramref name="to"/>, panel saat diliminde <c>yyyy-MM-ddTHH:mm</c>).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History(Guid id, string? range, string? from, string? to, CancellationToken cancellationToken)
    {
        var result = await _historyService.GetReportAsync(id, range, ParseLocal(from), ParseLocal(to), cancellationToken);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<ResourceHistoryModel>.Fail(result.Message ?? "Sunucu bulunamadı.", StatusCodes.Status404NotFound));

        return Ok(ApiResponse<ResourceHistoryModel>.Success(ResourceHistoryModel.Create(result.Data!)));
    }

    /// <summary>Verilen ana (UTC, ISO 8601) en yakın process anlık görüntüsü.</summary>
    [HttpGet]
    public async Task<IActionResult> ProcessSnapshot(Guid id, string? at, CancellationToken cancellationToken)
    {
        if (!DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var atUtc))
            return BadRequest(ApiResponse<ProcessSnapshotModel>.Fail("Geçersiz zaman.", StatusCodes.Status400BadRequest));

        var result = await _historyService.GetProcessSnapshotAsync(id, atUtc, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<ProcessSnapshotModel>.Fail(result.Message ?? "Kayıt bulunamadı.", StatusCodes.Status404NotFound));

        return Ok(ApiResponse<ProcessSnapshotModel>.Success(ProcessSnapshotModel.Create(result.Data!)));
    }

    private static DateTime? ParseLocal(string? value) =>
        DateTime.TryParseExact(value, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? AppTimeZone.ToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified))
            : null;

    [HttpGet]
    public async Task<IActionResult> Overview(Guid id, CancellationToken cancellationToken)
    {
        var canViewDocker = User.HasPermission(Permissions.DockerView);
        var result = await _resourceService.GetOverviewAsync(id, canViewDocker, cancellationToken);
        if (!result.IsSuccess)
            return Error(result);

        return PartialView("_Overview", new ServerResourcesPanelModel
        {
            ServerId = id,
            Overview = result.Data!,
            CanManageProcesses = User.HasPermission(Permissions.SystemManage),
            CanViewDocker = canViewDocker,
            CanCleanup = User.HasPermission(Permissions.ServerCleanup)
        });
    }

    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.SystemAction)]
    public async Task<IActionResult> Disk(Guid id, string? path, CancellationToken cancellationToken)
    {
        var result = await _resourceService.ScanDiskAsync(id, path, cancellationToken);
        return result.IsSuccess ? PartialView("_Disk", result.Data!) : Error(result);
    }

    private PartialViewResult Error(ServiceResult result)
    {
        Response.StatusCode = result.ErrorType == ServiceErrorType.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
        return PartialView(ErrorView, result.Message ?? "Sunucudan bilgi alınamadı.");
    }
}

using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Backups;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Api.V1;

/// <summary>Servisler (tek tıkla Docker servisleri).</summary>
[Route("api/v1/services")]
[HasPermission(Permissions.ServicesView)]
public sealed class ManagedServicesApiController : ApiV1ControllerBase
{
    private readonly IManagedServiceService _services;

    public ManagedServicesApiController(IManagedServiceService services)
    {
        _services = services;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiManagedService>>> List(Guid? serverId, CancellationToken cancellationToken)
    {
        var services = await _services.ListAsync(serverId, cancellationToken);
        return services.Select(ApiManagedService.From).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiManagedService>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _services.GetAsync(id, cancellationToken);
        return result.IsSuccess ? ApiManagedService.From(result.Data!) : Problem(result, "Servis bulunamadı");
    }

    /// <summary>Container'ın sunucudaki anlık durumu (SSH ile okunur).</summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<ApiManagedServiceStatus>> Status(Guid id, CancellationToken cancellationToken)
    {
        var result = await _services.GetRuntimeAsync(id, cancellationToken);
        return result.IsSuccess ? ApiManagedServiceStatus.From(id, result.Data!) : Problem(result, "Servis durumu okunamadı");
    }

    [HttpPost("{id:guid}/start")]
    [HasPermission(Permissions.ServicesManage)]
    public Task<ActionResult<ApiMessage>> Start(Guid id, CancellationToken cancellationToken) => RunAsync(id, DockerContainerAction.Start, cancellationToken);

    [HttpPost("{id:guid}/stop")]
    [HasPermission(Permissions.ServicesManage)]
    public Task<ActionResult<ApiMessage>> Stop(Guid id, CancellationToken cancellationToken) => RunAsync(id, DockerContainerAction.Stop, cancellationToken);

    [HttpPost("{id:guid}/restart")]
    [HasPermission(Permissions.ServicesManage)]
    public Task<ActionResult<ApiMessage>> Restart(Guid id, CancellationToken cancellationToken) => RunAsync(id, DockerContainerAction.Restart, cancellationToken);

    private async Task<ActionResult<ApiMessage>> RunAsync(Guid id, DockerContainerAction action, CancellationToken cancellationToken)
    {
        var result = await _services.ExecuteContainerActionAsync(id, action, cancellationToken);
        return result.IsSuccess ? new ApiMessage(result.Message ?? "İşlem tamamlandı.") : Problem(result, "Servis işlemi yapılamadı");
    }
}

[Route("api/v1/backups")]
[HasPermission(Permissions.BackupView)]
public sealed class BackupsApiController : ApiV1ControllerBase
{
    private readonly IBackupJobService _jobs;
    private readonly IBackupRunService _runs;
    private readonly IBackupDownloadService _downloads;
    private readonly BackupManager _backupManager;
    private readonly ICurrentUserService _currentUser;

    public BackupsApiController(
        IBackupJobService jobs,
        IBackupRunService runs,
        IBackupDownloadService downloads,
        BackupManager backupManager,
        ICurrentUserService currentUser)
    {
        _jobs = jobs;
        _runs = runs;
        _downloads = downloads;
        _backupManager = backupManager;
        _currentUser = currentUser;
    }

    private BackupActor Actor => new(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<ApiBackupJob>>> Jobs(Guid? serverId, CancellationToken cancellationToken)
    {
        var jobs = await _jobs.GetJobsAsync(serverId, cancellationToken);
        return jobs.Where(j => !j.IsDeleted).Select(ApiBackupJob.From).ToList();
    }

    /// <summary>İşi hemen çalıştırır (202). Durumu <c>GET /api/v1/backups/runs/{id}</c> ile izleyin.</summary>
    [HttpPost("jobs/{id:guid}/run")]
    [HasPermission(Permissions.BackupExecute)]
    [ProducesResponseType<ApiAccepted>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var result = await _backupManager.StartBackupAsync(id, BackupTrigger.Manual, Actor, cancellationToken);
        if (!result.IsSuccess)
            return Problem(result, "Yedekleme başlatılamadı");

        var url = $"/api/v1/backups/runs/{result.Data}";
        return Accepted(url, new ApiAccepted(result.Data, result.Message ?? "Yedekleme başlatıldı.", url));
    }

    [HttpGet("runs")]
    public async Task<ActionResult<ApiPage<ApiBackupRun>>> Runs(Guid? jobId, Guid? serverId, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var filter = new BackupRunFilterDto { JobId = jobId, ServerId = serverId, Page = NormalizePage(page), PageSize = NormalizePageSize(pageSize) };
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BackupRunStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return Problem(detail: $"Geçersiz durum: {status}", statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek");
            filter.Status = parsed;
        }

        var result = await _runs.SearchAsync(filter, cancellationToken);
        return ApiPage<ApiBackupRun>.From(result, ApiBackupRun.From);
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<ApiBackupRun>> GetRun(Guid id, CancellationToken cancellationToken)
    {
        var result = await _runs.GetAsync(id, includeLog: false, cancellationToken);
        return result.IsSuccess ? ApiBackupRun.From(result.Data!) : Problem(result, "Yedek bulunamadı");
    }

    /// <summary>Yedek dosyasını olduğu gibi (şifreliyse .smbk) akış olarak indirir; indirme audit log'a yazılır.</summary>
    [HttpGet("runs/{id:guid}/download")]
    [HasPermission(Permissions.BackupDownload)]
    [Produces("application/octet-stream", "application/problem+json")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await _downloads.OpenAsync(id, null, Actor, cancellationToken);
        if (!result.IsSuccess)
            return Problem(result, "Yedek indirilemedi");

        var download = result.Data!;
        Response.Headers.CacheControl = "no-store";
        if (download.Length is { } length)
            Response.ContentLength = length;

        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }
}

[Route("api/v1/alerts")]
[HasPermission(Permissions.AlertView)]
public sealed class AlertsApiController : ApiV1ControllerBase
{
    private readonly IAlertService _alerts;

    public AlertsApiController(IAlertService alerts)
    {
        _alerts = alerts;
    }

    /// <summary>Alarmlar. <paramref name="status"/>: <c>firing</c> (varsayılan, açık alarmlar), <c>resolved</c> veya <c>all</c>.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiPage<ApiAlert>>> List(string? status = "firing", Guid? serverId = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var filter = new AlertEventFilterDto { ServerId = serverId, Page = NormalizePage(page), PageSize = NormalizePageSize(pageSize) };
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<AlertEventStatus>(status ?? "firing", ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return Problem(detail: $"Geçersiz durum: {status}", statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek");
            filter.Status = parsed;
        }

        var result = await _alerts.SearchAsync(filter, cancellationToken);
        return ApiPage<ApiAlert>.From(result, ApiAlert.From);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [HasPermission(Permissions.AlertAcknowledge)]
    public async Task<ActionResult<ApiMessage>> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        var result = await _alerts.AcknowledgeAsync(id, cancellationToken);
        return result.IsSuccess ? new ApiMessage(result.Message ?? "Alarm üstlenildi.") : Problem(result, "Alarm üstlenilemedi");
    }
}

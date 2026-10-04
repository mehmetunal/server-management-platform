using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Backups;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.BackupView)]
public class BackupJobsController : Controller
{
    public const string FormOptionsKey = "BackupJobFormOptions";
    public const int RecentRunCount = 20;

    private readonly IBackupJobService _jobService;
    private readonly IBackupRunService _runService;
    private readonly IBackupStorageService _storageService;
    private readonly BackupManager _backupManager;
    private readonly ICurrentUserService _currentUser;
    private readonly BackupOptions _options;

    public BackupJobsController(
        IBackupJobService jobService,
        IBackupRunService runService,
        IBackupStorageService storageService,
        BackupManager backupManager,
        ICurrentUserService currentUser,
        IOptions<BackupOptions> options)
    {
        _jobService = jobService;
        _runService = runService;
        _storageService = storageService;
        _backupManager = backupManager;
        _currentUser = currentUser;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? serverId, CancellationToken cancellationToken)
    {
        var jobs = await _jobService.GetJobsAsync(serverId, cancellationToken);
        return Request.IsAjax() ? PartialView("_JobList", jobs) : View(jobs);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _jobService.GetDetailsAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        var model = new BackupJobDetailsViewModel
        {
            Details = result.Data!,
            RecentRuns = await _runService.GetRecentAsync(id, RecentRunCount, cancellationToken)
        };
        return Request.IsAjax() ? PartialView("_JobRuns", model) : View(model);
    }

    [HttpGet]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Create(Guid? serverId, CancellationToken cancellationToken)
    {
        await SetFormOptionsAsync(cancellationToken);
        return View(new BackupJobFormDto { ServerId = serverId });
    }

    [HttpPost]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Create(BackupJobFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _jobService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yedekleme işi eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id = result.Data }));
    }

    [HttpGet]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _jobService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        await SetFormOptionsAsync(cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Edit(Guid id, BackupJobFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _jobService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yedekleme işi güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id }));
    }

    [HttpPost]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _jobService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yedekleme işi silinemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpPost]
    [HasPermission(Permissions.BackupExecute)]
    [EnableRateLimiting(RateLimitPolicies.BackupAction)]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var actor = new BackupActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var result = await _backupManager.StartBackupAsync(id, BackupTrigger.Manual, actor, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yedekleme başlatılamadı.");

        return this.ApiSuccess(result.Message, Url.Action("Details", "BackupRuns", new { id = result.Data }));
    }

    private async Task SetFormOptionsAsync(CancellationToken cancellationToken)
    {
        ViewData[FormOptionsKey] = new BackupJobFormOptions
        {
            Servers = await _jobService.GetServerOptionsAsync(cancellationToken),
            Storages = await _storageService.GetOptionsAsync(cancellationToken),
            TimeZone = BackupSchedule.ResolveTimeZone(_options.TimeZone).Id
        };
    }
}

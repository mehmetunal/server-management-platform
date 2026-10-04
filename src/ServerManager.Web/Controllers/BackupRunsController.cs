using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Backups;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.BackupView)]
public class BackupRunsController : Controller
{
    public const string DownloadErrorKey = "BackupDownloadError";

    private readonly IBackupRunService _runService;
    private readonly IBackupJobService _jobService;
    private readonly BackupManager _backupManager;
    private readonly ICurrentUserService _currentUser;

    public BackupRunsController(IBackupRunService runService, IBackupJobService jobService, BackupManager backupManager, ICurrentUserService currentUser)
    {
        _runService = runService;
        _jobService = jobService;
        _backupManager = backupManager;
        _currentUser = currentUser;
    }

    private BackupActor Actor => new(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] BackupRunFilterDto filter, CancellationToken cancellationToken)
    {
        var list = new BackupRunListViewModel
        {
            Runs = await _runService.SearchAsync(filter, cancellationToken),
            Filter = filter
        };
        if (Request.IsAjax())
            return PartialView("_RunList", list);

        return View(new BackupRunIndexViewModel
        {
            List = list,
            Jobs = await _jobService.GetJobsAsync(null, cancellationToken),
            Servers = await _jobService.GetServerOptionsAsync(cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _runService.GetAsync(id, includeLog: true, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.BackupExecute)]
    public IActionResult Cancel(Guid id)
    {
        var result = _backupManager.Cancel(id, Actor);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İşlem iptal edilemedi.");
    }

    [HttpGet]
    [HasPermission(Permissions.BackupRestore)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await _runService.GetRestoreFormAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return result.ErrorType == ServiceErrorType.NotFound ? NotFound() : RedirectToAction(nameof(Details), new { id });

        return View(new BackupRestoreViewModel
        {
            Form = result.Data!,
            Servers = await _jobService.GetServerOptionsAsync(cancellationToken)
        });
    }

    [HttpPost]
    [HasPermission(Permissions.BackupRestore)]
    [EnableRateLimiting(RateLimitPolicies.BackupAction)]
    public async Task<IActionResult> Restore(Guid id, BackupRestoreDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.RunId = id;
        var result = await _backupManager.StartRestoreAsync(dto, Actor, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Geri yükleme başlatılamadı.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id = result.Data }));
    }

    /// <param name="decrypt">Şifreli yedeği panelde çözerek .tar.gz / .sql.gz olarak indirir.</param>
    [HttpGet]
    [HasPermission(Permissions.BackupRestore)]
    [EnableRateLimiting(RateLimitPolicies.BackupAction)]
    public async Task<IActionResult> Download(Guid id, bool decrypt, CancellationToken cancellationToken)
    {
        var result = await _runService.OpenDownloadAsync(id, decrypt, Actor, cancellationToken);
        if (!result.IsSuccess)
        {
            TempData[DownloadErrorKey] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        var download = result.Data!;
        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }

    [HttpPost]
    [HasPermission(Permissions.BackupManage)]
    public async Task<IActionResult> DeleteArtifact(Guid id, CancellationToken cancellationToken)
    {
        var result = await _runService.DeleteArtifactAsync(id, Actor, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yedek dosyası silinemedi.");

        return this.ApiSuccess(result.Message);
    }
}

using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Backups sekmesi: bu sunucunun yedekleme işleri ve yedek geçmişi.</summary>
[HasPermission(Permissions.BackupView)]
public class ServerBackupsController : Controller
{
    private readonly IBackupJobService _jobService;
    private readonly IBackupRunService _runService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerBackupsController(IBackupJobService jobService, IBackupRunService runService, ServerPageBuilder pageBuilder)
    {
        _jobService = jobService;
        _runService = runService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.BackupsTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        return View(new ServerBackupsViewModel
        {
            Page = page,
            Jobs = await _jobService.GetJobsAsync(id, cancellationToken),
            Runs = await RunListAsync(id, new BackupRunFilterDto(), cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Jobs(Guid id, CancellationToken cancellationToken) =>
        PartialView("~/Views/BackupJobs/_JobList.cshtml", await _jobService.GetJobsAsync(id, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Runs(Guid id, [FromQuery] BackupRunFilterDto filter, CancellationToken cancellationToken) =>
        PartialView("~/Views/BackupRuns/_RunList.cshtml", await RunListAsync(id, filter, cancellationToken));

    private async Task<BackupRunListViewModel> RunListAsync(Guid serverId, BackupRunFilterDto filter, CancellationToken cancellationToken)
    {
        filter.ServerId = serverId;
        return new BackupRunListViewModel { Runs = await _runService.SearchAsync(filter, cancellationToken), Filter = filter };
    }
}

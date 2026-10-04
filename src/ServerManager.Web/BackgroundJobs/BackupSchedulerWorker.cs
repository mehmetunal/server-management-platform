using Microsoft.Extensions.Options;
using ServerManager.Application.Backups;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Backups;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>Zamanı gelen yedekleme işlerini başlatır. Uygulama kapalıyken kaçan çalışmalar açılışta bir kez telafi edilir.</summary>
public sealed class BackupSchedulerWorker : BackgroundService
{
    private const int MaxJobsPerTick = 20;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly BackupManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupSchedulerWorker> _logger;

    public BackupSchedulerWorker(BackupManager manager, IServiceScopeFactory scopeFactory, IOptions<BackupOptions> options, ILogger<BackupSchedulerWorker> logger)
    {
        _manager = manager;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_options.Enabled)
                    await RunOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.SchedulerIntervalSeconds)), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> dueIds;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            dueIds = await scope.ServiceProvider.GetRequiredService<IBackupJobService>().ClaimDueJobsAsync(MaxJobsPerTick, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Zamanı gelen yedekleme işleri alınamadı.");
            return;
        }

        foreach (var jobId in dueIds)
        {
            try
            {
                var result = await _manager.StartBackupAsync(jobId, BackupTrigger.Scheduled, BackupActor.System, stoppingToken);
                if (!result.IsSuccess)
                    _logger.LogWarning("Zamanlanmış yedekleme başlatılamadı. JobId: {JobId}, Reason: {Reason}", jobId, result.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Zamanlanmış yedekleme başlatılamadı. JobId: {JobId}", jobId);
            }
        }
    }
}

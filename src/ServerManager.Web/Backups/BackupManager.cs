using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Backups;

/// <summary>
/// Yedekleme ve geri yüklemeleri HTTP isteğinden bağımsız arka planda çalıştırır. Aynı anda en fazla
/// <see cref="BackupOptions.MaxConcurrency"/> işlem aktarım yapar, diğerleri sırada bekler. Aynı iş için tek yedekleme çalışır.
/// </summary>
public sealed class BackupManager : IDisposable
{
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, BackupActiveRun> _runs = new();
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly SemaphoreSlim _concurrency;
    private readonly CancellationTokenSource _stopping = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupManager> _logger;

    public BackupManager(IServiceScopeFactory scopeFactory, IOptions<BackupOptions> options, ILogger<BackupManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var limit = Math.Clamp(options.Value.MaxConcurrency, 1, 16);
        _concurrency = new SemaphoreSlim(limit, limit);
    }

    public async Task<ServiceResult<Guid>> StartBackupAsync(Guid jobId, BackupTrigger trigger, BackupActor actor, CancellationToken cancellationToken)
    {
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_runs.Values.Any(r => r.JobId == jobId && !r.IsCompleted))
                return ServiceResult<Guid>.Failure("Bu iş için süren bir yedekleme var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

            ServiceResult<Guid> result;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                result = await scope.ServiceProvider.GetRequiredService<IBackupRunService>().BeginBackupAsync(jobId, trigger, actor, cancellationToken);
            }

            if (result.IsSuccess)
                Launch(new BackupActiveRun(result.Data, jobId, new BackupCancellation(_stopping.Token)),
                    (service, run) => service.RunBackupAsync(run.Id, actor, run.Cancellation));
            return result;
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task<ServiceResult<Guid>> StartRestoreAsync(BackupRestoreDto dto, BackupActor actor, CancellationToken cancellationToken)
    {
        ServiceResult<Guid> result;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<IBackupRunService>().BeginRestoreAsync(dto, actor, cancellationToken);
        }

        if (result.IsSuccess)
            Launch(new BackupActiveRun(result.Data, null, new BackupCancellation(_stopping.Token)),
                (service, run) => service.RunRestoreAsync(run.Id, dto, actor, run.Cancellation));
        return result;
    }

    public bool IsRunning(Guid runId) => _runs.TryGetValue(runId, out var run) && !run.IsCompleted;

    public ServiceResult Cancel(Guid runId, BackupActor actor)
    {
        if (!_runs.TryGetValue(runId, out var run) || run.IsCompleted)
            return ServiceResult.Failure("İşlem çalışmıyor veya zaten bitti.", ServiceErrorType.Conflict);

        return run.Cancellation.Cancel(actor)
            ? ServiceResult.Success("İptal isteği gönderildi; aktarım durduruluyor.")
            : ServiceResult.Failure("İptal isteği zaten gönderildi.", ServiceErrorType.Conflict);
    }

    /// <summary>Uygulama kapanırken süren işlemleri durdurur; kayıtlar "kesildi" olarak işaretlenir.</summary>
    public async Task StopAllAsync()
    {
        if (_runs.IsEmpty)
            return;

        await _stopping.CancelAsync();
        var running = _runs.Values.Select(r => r.Execution).ToArray();
        await Task.WhenAny(Task.WhenAll(running), Task.Delay(ShutdownWait));
    }

    public void Dispose()
    {
        _stopping.Dispose();
        _startGate.Dispose();
        _concurrency.Dispose();
    }

    private void Launch(BackupActiveRun run, Func<IBackupRunService, BackupActiveRun, Task<ServiceResult>> execute)
    {
        _runs[run.Id] = run;
        run.Execution = Task.Run(() => ExecuteAsync(run, execute), CancellationToken.None);
    }

    private async Task ExecuteAsync(BackupActiveRun run, Func<IBackupRunService, BackupActiveRun, Task<ServiceResult>> execute)
    {
        var acquired = false;
        try
        {
            try
            {
                await _concurrency.WaitAsync(run.Cancellation.Token);
                acquired = true;
            }
            catch (OperationCanceledException)
            {
                // Sırada beklerken iptal edildi; servis kaydı iptal/kesildi olarak kapatır.
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            await execute(scope.ServiceProvider.GetRequiredService<IBackupRunService>(), run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yedek işlemi çalıştırılamadı. RunId: {RunId}", run.Id);
        }
        finally
        {
            if (acquired)
                _concurrency.Release();
            _runs.TryRemove(run.Id, out _);
            run.Cancellation.Dispose();
        }
    }
}

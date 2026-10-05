using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Backups;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Açılışta önceki çalışmadan yarım kalan yedek/geri yükleme kayıtlarını "kesildi" olarak işaretler (kayıt silinmez);
/// kapanışta süren işlemleri durdurur.
/// </summary>
public sealed class BackupLifecycleWorker : BackgroundService
{
    private readonly BackupManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupLifecycleWorker> _logger;

    public BackupLifecycleWorker(BackupManager manager, IServiceScopeFactory scopeFactory, ILogger<BackupLifecycleWorker> logger)
    {
        _manager = manager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<IBackupRunService>().InterruptRunningAsync(stoppingToken);
            if (count > 0)
                _logger.LogWarning("Önceki çalışmadan yarım kalan {Count} yedek işlemi kesildi olarak işaretlendi.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Yarım kalan yedek kayıtları işaretlenemedi.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.StopAllAsync();
        await base.StopAsync(cancellationToken);
    }
}

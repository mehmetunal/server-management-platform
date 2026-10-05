using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.ManagedServices;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Açılışta önceki çalışmadan yarım kalan servis işlemlerini "kesildi" olarak işaretler; kapanışta süren işlemleri durdurur.
/// </summary>
public sealed class ManagedServiceLifecycleWorker : BackgroundService
{
    private readonly ManagedServiceManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ManagedServiceLifecycleWorker> _logger;

    public ManagedServiceLifecycleWorker(ManagedServiceManager manager, IServiceScopeFactory scopeFactory, ILogger<ManagedServiceLifecycleWorker> logger)
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
            var count = await scope.ServiceProvider.GetRequiredService<IManagedServiceService>().InterruptRunningAsync(stoppingToken);
            if (count > 0)
                _logger.LogWarning("Önceki çalışmadan yarım kalan {Count} servis işlemi kesildi olarak işaretlendi.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Yarım kalan servis işlemleri işaretlenemedi.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.StopAllAsync();
        await base.StopAsync(cancellationToken);
    }
}

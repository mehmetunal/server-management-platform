using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Deployments;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Açılışta önceki çalışmadan yarım kalan deployment kayıtlarını "kesildi" olarak işaretler (kayıt silinmez);
/// kapanışta süren deployment'ları durdurur.
/// </summary>
public sealed class DeploymentLifecycleWorker : BackgroundService
{
    private readonly DeploymentManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeploymentLifecycleWorker> _logger;

    public DeploymentLifecycleWorker(DeploymentManager manager, IServiceScopeFactory scopeFactory, ILogger<DeploymentLifecycleWorker> logger)
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
            var count = await scope.ServiceProvider.GetRequiredService<IDeploymentService>().InterruptRunningAsync(stoppingToken);
            if (count > 0)
                _logger.LogWarning("Önceki çalışmadan yarım kalan {Count} deployment kesildi olarak işaretlendi.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Yarım kalan deployment kayıtları işaretlenemedi.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.StopAllAsync();
        await base.StopAsync(cancellationToken);
    }
}

using Microsoft.Extensions.Options;
using ServerManager.Application.Dokploy;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Dokploy;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Açılışta yarım kalan kurulum kayıtlarını "kesildi" olarak işaretler, ardından kayıtlı Dokploy
/// örneklerinin durumunu belirli aralıklarla kontrol eder.
/// </summary>
public sealed class DokployHealthWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly DokployInstallationManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DokployOptions _options;
    private readonly ILogger<DokployHealthWorker> _logger;

    public DokployHealthWorker(
        DokployInstallationManager manager,
        IServiceScopeFactory scopeFactory,
        IOptions<DokployOptions> options,
        ILogger<DokployHealthWorker> logger)
    {
        _manager = manager;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InterruptOrphanedInstallationsAsync(stoppingToken);

        if (_options.HealthCheckIntervalMinutes <= 0)
            return;

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.HealthCheckIntervalMinutes));
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IDokployService>().RunScheduledHealthChecksAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Dokploy sağlık kontrolleri çalıştırılamadı.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.StopAllAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task InterruptOrphanedInstallationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<IDokployService>().InterruptRunningInstallationsAsync(cancellationToken);
            if (count > 0)
                _logger.LogWarning("{Count} yarım kalmış Dokploy kurulum kaydı kesildi olarak işaretlendi.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Yarım kalmış Dokploy kurulumları işaretlenemedi.");
        }
    }
}

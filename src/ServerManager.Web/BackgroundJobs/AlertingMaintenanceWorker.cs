using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>Yalnızca geçici kayıtları (uptime sonuçları, bildirim gönderimleri) saklama süresine göre temizler.</summary>
public sealed class AlertingMaintenanceWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertingMaintenanceWorker> _logger;

    public AlertingMaintenanceWorker(IServiceScopeFactory scopeFactory, ILogger<AlertingMaintenanceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IUptimeService>().RunMaintenanceAsync(stoppingToken);
            await scope.ServiceProvider.GetRequiredService<IAlertService>().RunMaintenanceAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Alarm bakımı (saklama süresi temizliği) başarısız.");
        }
    }
}

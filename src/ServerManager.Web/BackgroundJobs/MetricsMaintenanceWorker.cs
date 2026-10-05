using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>Saatlik metrik özetini üretir; metrik, sağlık kontrolü ve panelden süresi verilen geçmiş/log kayıtlarını temizler.</summary>
public sealed class MetricsMaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonitoringOptions _options;
    private readonly ILogger<MetricsMaintenanceWorker> _logger;

    public MetricsMaintenanceWorker(IServiceScopeFactory scopeFactory, IOptions<MonitoringOptions> options, ILogger<MetricsMaintenanceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            // Aralık her turda yeniden okunur; ayar değişikliği yeniden başlatma gerektirmez.
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.MaintenanceIntervalMinutes)), stoppingToken);
            }
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
            await scope.ServiceProvider.GetRequiredService<IMonitoringService>().RunMaintenanceAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Metrik bakımı (aggregation/retention) başarısız.");
        }
    }
}

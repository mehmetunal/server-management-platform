using Microsoft.Extensions.Options;
using ServerManager.Application.Cloud;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Bulut hesaplarını aralıkla eşitler: IP'si eşleşen sunucular bağlanır, bağlı sunucuların aylık maliyeti güncellenir.
/// </summary>
public sealed class CloudSyncWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CloudOptions _options;
    private readonly ILogger<CloudSyncWorker> _logger;
    private DateTime _lastSync = DateTime.MinValue;

    public CloudSyncWorker(IServiceScopeFactory scopeFactory, IOptions<CloudOptions> options, ILogger<CloudSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        // Panel ayarları IOptions<T> örneğini değiştirir; IOptionsMonitor ayrı örnek tuttuğu için değişikliği görmez.
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

            using var timer = new PeriodicTimer(TickInterval);
            do
            {
                await SyncIfDueAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SyncIfDueAsync(CancellationToken stoppingToken)
    {
        var intervalHours = _options.SyncIntervalHours;
        if (intervalHours <= 0 || DateTime.UtcNow - _lastSync < TimeSpan.FromHours(intervalHours))
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var synced = await scope.ServiceProvider.GetRequiredService<ICloudAccountService>().SyncAllAsync(stoppingToken);
            if (synced > 0)
                _logger.LogInformation("Bulut hesapları eşitlendi. Accounts: {Count}", synced);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Bulut hesapları eşitlenemedi.");
        }
        finally
        {
            _lastSync = DateTime.UtcNow;
        }
    }
}

using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.BackgroundJobs;

public sealed class MetricsCollectorWorker : BackgroundService
{
    private const int MinimumIntervalSeconds = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonitoringOptions _options;
    private readonly ILogger<MetricsCollectorWorker> _logger;

    public MetricsCollectorWorker(IServiceScopeFactory scopeFactory, IOptions<MonitoringOptions> options, ILogger<MetricsCollectorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(MinimumIntervalSeconds, _options.IntervalSeconds));
        _logger.LogInformation(
            "Metrik toplayıcı başladı. Interval: {IntervalSeconds} sn, MaxConcurrency: {MaxConcurrency}",
            interval.TotalSeconds, MaxConcurrency);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            using var timer = new PeriodicTimer(interval);
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private int MaxConcurrency => Math.Clamp(_options.MaxConcurrency, 1, 32);

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> serverIds;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            serverIds = await scope.ServiceProvider.GetRequiredService<IMonitoringService>().GetCollectableServerIdsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "İzlenecek sunucu listesi alınamadı.");
            return;
        }

        if (serverIds.Count == 0)
            return;

        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = serverIds.Select(async serverId =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
                await monitoringService.CollectAsync(serverId, manual: false, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Metrik toplama başarısız. ServerId: {ServerId}", serverId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}

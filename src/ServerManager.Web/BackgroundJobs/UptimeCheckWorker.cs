using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Her 10 saniyede zamanı gelen kontrolleri başlatır; yavaş bir kontrol diğerlerini bekletmez ve aynı kontrol
/// önceki çalışması bitmeden tekrar başlatılmaz.
/// </summary>
public sealed class UptimeCheckWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlertingOptions _options;
    private readonly ILogger<UptimeCheckWorker> _logger;
    private readonly ConcurrentDictionary<Guid, Task> _running = new();

    public UptimeCheckWorker(IServiceScopeFactory scopeFactory, IOptions<AlertingOptions> options, ILogger<UptimeCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxConcurrency = Math.Clamp(_options.UptimeMaxConcurrency, 1, 64);
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        _logger.LogInformation("Uptime kontrolcüsü başladı. MaxConcurrency: {MaxConcurrency}", maxConcurrency);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            using var timer = new PeriodicTimer(TickInterval);
            do
            {
                await StartDueChecksAsync(semaphore, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(_running.Values).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    private async Task StartDueChecksAsync(SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> dueIds;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            dueIds = await scope.ServiceProvider.GetRequiredService<IUptimeService>().GetDueCheckIdsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Zamanı gelen uptime kontrolleri alınamadı.");
            return;
        }

        foreach (var id in dueIds.Where(id => !_running.ContainsKey(id)))
        {
            // Görev sözlüğe yazılmadan biterse TryRemove boşa çalışır ve kontrol bir daha başlatılmaz; bu yüzden kayıttan sonra başlar.
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _running[id] = RunAsync(id, start.Task, semaphore, stoppingToken);
            start.SetResult();
        }
    }

    private async Task RunAsync(Guid id, Task start, SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        await start;
        try
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IUptimeService>().RunCheckAsync(id, stoppingToken);
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uptime kontrolü başarısız. CheckId: {CheckId}", id);
        }
        finally
        {
            _running.TryRemove(id, out _);
        }
    }
}

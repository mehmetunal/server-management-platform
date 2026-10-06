using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Kaynak geçmişi: <c>Monitoring:ResourceHistoryIntervalMinutes</c> aralıkla (varsayılan 5 dk) izlenen sunuculardan container
/// kullanım/durum örnekleri ve en çok kaynak kullanan process'leri toplar, saatlik özetleri üretir. Ana metrik toplayıcıyı
/// yavaşlatmamak için ayrı çalışır. Düşük sıklıkta (<c>Monitoring:ReclaimableScanIntervalHours</c>) temizlik taraması yapar
/// (silme yok) ve "Temizlenebilir alan" alarmı için sonucu kaydeder.
/// </summary>
public sealed class ResourceHistoryWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private const int MaxConcurrency = 4;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MonitoringOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResourceHistoryWorker> _logger;
    private readonly Dictionary<Guid, DateTime> _lastScanAttempts = [];
    private DateTime _nextCollection = DateTime.MinValue;

    public ResourceHistoryWorker(IServiceScopeFactory scopeFactory, IOptions<MonitoringOptions> options, TimeProvider timeProvider, ILogger<ResourceHistoryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        // Panel ayarları IOptions<T> örneğini değiştirir; değerler her turda yeniden okunur.
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(TickInterval);
            do
            {
                if (!_options.Enabled)
                    continue;

                if (_options.ResourceHistoryIntervalMinutes > 0 && UtcNow >= _nextCollection)
                {
                    _nextCollection = UtcNow.AddMinutes(_options.ResourceHistoryIntervalMinutes);
                    await CollectAsync(stoppingToken);
                }

                if (_options.ReclaimableScanIntervalHours > 0)
                    await ScanReclaimableAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CollectAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> serverIds;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            serverIds = await scope.ServiceProvider.GetRequiredService<IResourceHistoryService>().GetCollectableServerIdsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Kaynak geçmişi için sunucu listesi alınamadı.");
            return;
        }

        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        await Task.WhenAll(serverIds.Select(async serverId =>
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IResourceHistoryService>().CollectAsync(serverId, stoppingToken);
                if (!result.IsSuccess)
                    _logger.LogDebug("Kaynak geçmişi toplanamadı. ServerId: {ServerId}, Reason: {Reason}", serverId, result.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Kaynak geçmişi toplanamadı. ServerId: {ServerId}", serverId);
            }
            finally
            {
                semaphore.Release();
            }
        }));

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IResourceHistoryService>().AggregateHourlyAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Container saatlik özetleri üretilemedi.");
        }
    }

    private async Task ScanReclaimableAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> due;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            due = await scope.ServiceProvider.GetRequiredService<IResourceHistoryService>().GetReclaimableScanDueServerIdsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Temizlik taraması zamanı gelen sunucular alınamadı.");
            return;
        }

        // Başarısız tarama (erişilemeyen sunucu) kayıt yazmaz; aralık dolmadan yeniden denenmez.
        var retryAfter = TimeSpan.FromHours(_options.ReclaimableScanIntervalHours);
        foreach (var serverId in due)
        {
            if (_lastScanAttempts.TryGetValue(serverId, out var attempted) && UtcNow - attempted < retryAfter)
                continue;

            _lastScanAttempts[serverId] = UtcNow;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IResourceHistoryService>().ScanReclaimableAsync(serverId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Temizlenebilir alan taraması yapılamadı. ServerId: {ServerId}", serverId);
            }
        }
    }
}

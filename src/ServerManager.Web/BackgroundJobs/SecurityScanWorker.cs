using Microsoft.Extensions.Options;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Security;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>
/// Açılışta yarım kalan taramaları başarısız olarak işaretler (kayıt silinmez), aralığı dolan sunucuları sırayla tarar
/// ve saklama süresi dolan tarama kayıtlarını (sunucu başına son N korunarak) temizler.
/// </summary>
public sealed class SecurityScanWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SecurityScanOptions> _options;
    private readonly ILogger<SecurityScanWorker> _logger;
    private DateTime _lastMaintenance = DateTime.MinValue;

    public SecurityScanWorker(IServiceScopeFactory scopeFactory, IOptionsMonitor<SecurityScanOptions> options, ILogger<SecurityScanWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InterruptRunningAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            using var timer = new PeriodicTimer(TickInterval);
            do
            {
                await RunMaintenanceAsync(stoppingToken);
                await RunDueScansAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task InterruptRunningAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<ISecurityService>().InterruptRunningAsync(stoppingToken);
            if (count > 0)
                _logger.LogWarning("Önceki çalışmadan yarım kalan {Count} güvenlik taraması başarısız olarak işaretlendi.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Yarım kalan güvenlik taramaları işaretlenemedi.");
        }
    }

    private async Task RunMaintenanceAsync(CancellationToken stoppingToken)
    {
        if (DateTime.UtcNow - _lastMaintenance < MaintenanceInterval)
            return;

        try
        {
            var options = _options.CurrentValue;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<ISecurityService>()
                .DeleteExpiredAsync(options.RetentionDays, options.KeepLatestPerServer, stoppingToken);
            if (deleted > 0)
                _logger.LogInformation("Eski güvenlik taraması kayıtları temizlendi. Deleted: {Deleted}", deleted);
            _lastMaintenance = DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Güvenlik taraması kayıtları temizlenemedi.");
        }
    }

    private async Task RunDueScansAsync(CancellationToken stoppingToken)
    {
        var intervalHours = _options.CurrentValue.ScanIntervalHours;
        if (intervalHours <= 0)
            return;

        IReadOnlyList<Guid> dueIds;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            dueIds = await scope.ServiceProvider.GetRequiredService<ISecurityService>()
                .GetDueServerIdsAsync(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Taranacak sunucular alınamadı.");
            return;
        }

        foreach (var serverId in dueIds)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISecurityService>()
                    .ScanAsync(serverId, SecurityScanTrigger.Scheduled, stoppingToken);
                if (!result.IsSuccess)
                    _logger.LogWarning("Zamanlanmış güvenlik taraması başarısız. ServerId: {ServerId}, Reason: {Reason}", serverId, result.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Zamanlanmış güvenlik taraması çalıştırılamadı. ServerId: {ServerId}", serverId);
            }
        }
    }
}

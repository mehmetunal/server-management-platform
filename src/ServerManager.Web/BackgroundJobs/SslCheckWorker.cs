using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.BackgroundJobs;

public sealed class SslCheckWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlertingOptions _options;
    private readonly ILogger<SslCheckWorker> _logger;

    public SslCheckWorker(IServiceScopeFactory scopeFactory, IOptions<AlertingOptions> options, ILogger<SslCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            using var timer = new PeriodicTimer(TickInterval);
            do
            {
                if (_options.Enabled)
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
        IReadOnlyList<Guid> dueIds;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            dueIds = await scope.ServiceProvider.GetRequiredService<ISslCertificateService>().GetDueIdsAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Zamanı gelen SSL kontrolleri alınamadı.");
            return;
        }

        foreach (var id in dueIds)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISslCertificateService>().RunCheckAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "SSL kontrolü başarısız. MonitorId: {MonitorId}", id);
            }
        }
    }
}

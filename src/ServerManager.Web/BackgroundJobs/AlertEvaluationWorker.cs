using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.BackgroundJobs;

public sealed class AlertEvaluationWorker : BackgroundService
{
    private const int MinimumIntervalSeconds = 15;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlertingOptions _options;
    private readonly ILogger<AlertEvaluationWorker> _logger;

    public AlertEvaluationWorker(IServiceScopeFactory scopeFactory, IOptions<AlertingOptions> options, ILogger<AlertEvaluationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Alarm değerlendirici başladı. Interval: {IntervalSeconds} sn",
            Math.Max(MinimumIntervalSeconds, _options.EvaluationIntervalSeconds));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_options.Enabled)
                    await RunOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(MinimumIntervalSeconds, _options.EvaluationIntervalSeconds)), stoppingToken);
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
            await scope.ServiceProvider.GetRequiredService<IAlertService>().EvaluateAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Alarm değerlendirmesi başarısız.");
        }
    }
}

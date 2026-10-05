using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Terminal;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>Boşta kalan, yeniden bağlanılmayan ve onayı zaman aşımına uğrayan terminal oturumlarını kapatır.</summary>
public sealed class TerminalIdleSweeper : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly TerminalManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TerminalIdleSweeper> _logger;

    public TerminalIdleSweeper(TerminalManager manager, IServiceScopeFactory scopeFactory, ILogger<TerminalIdleSweeper> logger)
    {
        _manager = manager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CloseOrphanedSessionsAsync(stoppingToken);

        try
        {
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var closed = await _manager.SweepAsync(DateTime.UtcNow);
                    if (closed > 0)
                        _logger.LogInformation("{Count} terminal oturumu kapatıldı.", closed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Terminal oturumları denetlenemedi.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.CloseAllAsync("Uygulama kapatılıyor.");
        await base.StopAsync(cancellationToken);
    }

    private async Task CloseOrphanedSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITerminalService>();
            var count = await service.CloseOpenSessionsAsync(_startedAt, "Uygulama yeniden başlatıldı.", cancellationToken);
            if (count > 0)
                _logger.LogInformation("Önceki çalışmadan açık kalan {Count} terminal oturum kaydı kapatıldı.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Açık kalan terminal oturum kayıtları kapatılamadı.");
        }
    }
}

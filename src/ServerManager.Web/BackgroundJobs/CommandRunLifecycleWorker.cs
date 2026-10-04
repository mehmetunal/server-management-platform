using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Commands;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>Açılışta önceki çalışmadan yarım kalan toplu komut kayıtlarını "kesildi" olarak işaretler; kapanışta süren komutları durdurur.</summary>
public sealed class CommandRunLifecycleWorker : BackgroundService
{
    private readonly CommandRunManager _manager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandRunLifecycleWorker> _logger;

    public CommandRunLifecycleWorker(CommandRunManager manager, IServiceScopeFactory scopeFactory, ILogger<CommandRunLifecycleWorker> logger)
    {
        _manager = manager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<ICommandRunService>().InterruptRunningAsync(stoppingToken);
            if (count > 0)
                _logger.LogWarning("Önceki çalışmadan yarım kalan {Count} toplu komut kesildi olarak işaretlendi.", count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Yarım kalan toplu komut kayıtları işaretlenemedi.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.StopAllAsync();
        await base.StopAsync(cancellationToken);
    }
}

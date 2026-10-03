using Microsoft.Extensions.Options;
using ServerManager.Application.Docker;
using ServerManager.Web.Services;

namespace ServerManager.Web.BackgroundJobs;

public sealed class TerminalIdleSweeper : BackgroundService
{
    private readonly ContainerTerminalManager _manager;
    private readonly DockerOptions _options;
    private readonly ILogger<TerminalIdleSweeper> _logger;

    public TerminalIdleSweeper(ContainerTerminalManager manager, IOptions<DockerOptions> options, ILogger<TerminalIdleSweeper> logger)
    {
        _manager = manager;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.TerminalIdleTimeoutMinutes));

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var closed = await _manager.CloseIdleAsync(idleTimeout);
                    if (closed > 0)
                        _logger.LogInformation("Boşta kalan {Count} terminal oturumu kapatıldı.", closed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Boşta kalan terminal oturumları kapatılamadı.");
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
}

using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Terminal;

namespace ServerManager.Web.BackgroundJobs;

/// <summary>Terminal komut kuyruğunu toplu olarak veritabanına yazar. Kapanışta kuyrukta kalanlar da yazılır.</summary>
public sealed class TerminalCommandWriter : BackgroundService
{
    private const int BatchSize = 100;

    private readonly TerminalCommandQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TerminalCommandWriter> _logger;

    public TerminalCommandWriter(TerminalCommandQueue queue, IServiceScopeFactory scopeFactory, ILogger<TerminalCommandWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<TerminalCommandEntry>(BatchSize);
        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < BatchSize && _queue.Reader.TryRead(out var entry))
                    batch.Add(entry);

                await WriteAsync(batch);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        var batch = new List<TerminalCommandEntry>(BatchSize);
        while (_queue.Reader.TryRead(out var entry))
        {
            batch.Add(entry);
            if (batch.Count == BatchSize)
                await WriteAsync(batch);
        }

        await WriteAsync(batch);
    }

    private async Task WriteAsync(List<TerminalCommandEntry> batch)
    {
        if (batch.Count == 0)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITerminalService>();
            await service.RecordCommandsAsync(batch.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Count} terminal komut kaydı yazılamadı.", batch.Count);
        }
        finally
        {
            batch.Clear();
        }
    }
}

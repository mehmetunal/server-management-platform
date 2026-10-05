using System.Collections.Concurrent;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Commands;
using ServerManager.Application.Interfaces.Services;

namespace ServerManager.Web.Commands;

/// <summary>Toplu komutları HTTP isteğinden bağımsız arka planda çalıştırır; kapanışta süren işlemler "kesildi" olarak kapanır.</summary>
public sealed class CommandRunManager : IDisposable
{
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, Task> _runs = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandRunManager> _logger;

    public CommandRunManager(IServiceScopeFactory scopeFactory, ILogger<CommandRunManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Kaydı isteği yapan kullanıcının kapsamında oluşturur (denetim kaydı için), çalıştırmayı arka plana bırakır.</summary>
    public async Task<ServiceResult<Guid>> StartAsync(ICommandRunService service, CommandRunRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await service.BeginAsync(dto, cancellationToken);
        if (result.IsSuccess)
        {
            var runId = result.Data;

            // Görev sözlüğe yazılmadan biterse TryRemove boşa çalışır ve IsRunning hep true kalır; bu yüzden kayıttan sonra başlar.
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _runs[runId] = Task.Run(() => ExecuteAsync(runId, start.Task), CancellationToken.None);
            start.SetResult();
        }

        return result;
    }

    public bool IsRunning(Guid runId) => _runs.ContainsKey(runId);

    public async Task StopAllAsync()
    {
        if (_runs.IsEmpty)
            return;

        await _stopping.CancelAsync();
        await Task.WhenAny(Task.WhenAll(_runs.Values.ToArray()), Task.Delay(ShutdownWait));
    }

    public void Dispose() => _stopping.Dispose();

    private async Task ExecuteAsync(Guid runId, Task start)
    {
        await start;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandRunService>().ExecuteAsync(runId, _stopping.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toplu komut çalıştırılamadı. RunId: {RunId}", runId);
        }
        finally
        {
            _runs.TryRemove(runId, out _);
        }
    }
}

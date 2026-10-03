using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.Dokploy;

/// <summary>
/// Dokploy kurulumlarını HTTP isteğinden bağımsız arka planda çalıştırır.
/// Tarayıcı kapansa da kurulum sürer; yeniden açılan sayfa <see cref="Snapshot"/> ile kaldığı yerden izler.
/// </summary>
public sealed class DokployInstallationManager : IDisposable
{
    private const int OutputBufferCapacity = 256 * 1024;
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, DokployInstallationRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _serverGates = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly IHubContext<DokployHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DokployInstallationManager> _logger;

    public DokployInstallationManager(IHubContext<DokployHub> hubContext, IServiceScopeFactory scopeFactory, ILogger<DokployInstallationManager> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ServiceResult<Guid>> StartAsync(Guid serverId, DokployInstallRequestDto dto, DokployActor actor, CancellationToken cancellationToken)
    {
        var gate = _serverGates.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.Zero, cancellationToken))
            return ServiceResult<Guid>.Failure("Bu sunucu için kurulum zaten başlatılıyor.", ServiceErrorType.Conflict);

        try
        {
            if (_runs.Values.Any(r => r.ServerId == serverId && !r.IsCompleted))
                return ServiceResult<Guid>.Failure("Bu sunucuda devam eden bir Dokploy kurulumu var.", ServiceErrorType.Conflict);

            ServiceResult<Guid> result;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IDokployService>();
                result = await service.BeginInstallationAsync(serverId, dto, actor, cancellationToken);
            }

            if (!result.IsSuccess)
                return result;

            var run = new DokployInstallationRun(result.Data, serverId, OutputBufferCapacity);
            _runs[run.Id] = run;
            run.Execution = Task.Run(() => ExecuteAsync(run, actor), CancellationToken.None);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Bellekte izlenen kurulumun anlık görüntüsü; kurulum bu sunucuya ait değilse veya bellekte yoksa null.</summary>
    public DokployJoinResponse? Snapshot(Guid serverId, Guid installationId) =>
        _runs.TryGetValue(installationId, out var run) && run.ServerId == serverId ? run.Snapshot() : null;

    /// <summary>Uygulama kapanırken süren kurulumların takibini keser; kayıtlar "kesildi" olarak işaretlenir.</summary>
    public async Task StopAllAsync()
    {
        if (_runs.IsEmpty)
            return;

        await _stopping.CancelAsync();
        var running = _runs.Values.Select(r => r.Execution).ToArray();
        await Task.WhenAny(Task.WhenAll(running), Task.Delay(ShutdownWait));
    }

    public void Dispose() => _stopping.Dispose();

    private async Task ExecuteAsync(DokployInstallationRun run, DokployActor actor)
    {
        var sink = new DokployInstallationSink(run, _hubContext, _logger);
        ServiceResult result;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IDokployService>();
            result = await service.RunInstallationAsync(run.Id, actor, sink, _stopping.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dokploy kurulumu çalıştırılamadı. InstallationId: {InstallationId}", run.Id);
            result = ServiceResult.Failure("Kurulum beklenmeyen bir hata ile durdu.");
        }

        run.Complete(result.IsSuccess, result.Message);
        await sink.CompletedAsync(result.IsSuccess, result.Message);
        _ = ForgetLaterAsync(run.Id);
    }

    private async Task ForgetLaterAsync(Guid installationId)
    {
        try
        {
            await Task.Delay(CompletedRetention, _stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _runs.TryRemove(installationId, out _);
    }
}

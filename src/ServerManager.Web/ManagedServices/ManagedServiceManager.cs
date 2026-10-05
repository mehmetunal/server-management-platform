using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Web.Hubs;
using ServerManager.Web.Services;

namespace ServerManager.Web.ManagedServices;

/// <summary>
/// Servis işlemlerini (kurulum, yeniden oluşturma, yükseltme, kaldırma) HTTP isteğinden bağımsız arka planda çalıştırır.
/// Tarayıcı kapansa da işlem sürer; yeniden açılan sayfa <see cref="Snapshot"/> ile kaldığı yerden izler.
/// Aynı serviste aynı anda tek işlem çalışır.
/// </summary>
public sealed class ManagedServiceManager : IDisposable
{
    private const int OutputBufferCapacity = 512 * 1024;
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, ServiceOperationRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _serviceGates = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly IHubContext<ServiceHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ManagedServiceManager> _logger;

    public ManagedServiceManager(IHubContext<ServiceHub> hubContext, IServiceScopeFactory scopeFactory, ILogger<ManagedServiceManager> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <param name="serviceId">Var olan servis üzerindeki işlemlerde kilit için; kurulumda null.</param>
    /// <param name="onSucceeded">
    /// İşlem başarıyla biterse (yalnızca o zaman) yeni bir DI kapsamında, işlemi başlatan kullanıcı adına çalışır (ör. otomatik yedek işi).
    /// Dönen metin (<see cref="ServiceConsole"/> biçiminde) canlı çıktıya yazılır; hata işlemin sonucunu değiştirmez.
    /// </param>
    public async Task<ServiceResult<ServiceOperationStart>> StartAsync(
        Guid? serviceId,
        ServiceActor actor,
        Func<IManagedServiceService, CancellationToken, Task<ServiceResult<ServiceOperationStart>>> begin,
        CancellationToken cancellationToken,
        Func<IServiceProvider, Guid, CancellationToken, Task<string?>>? onSucceeded = null)
    {
        var gate = serviceId is { } id ? _serviceGates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1)) : null;
        if (gate is not null && !await gate.WaitAsync(TimeSpan.Zero, cancellationToken))
            return ServiceResult<ServiceOperationStart>.Failure("Bu servis için işlem zaten başlatılıyor.", ServiceErrorType.Conflict);

        try
        {
            if (serviceId is { } existing && _runs.Values.Any(r => r.ServiceId == existing && !r.IsCompleted))
                return ServiceResult<ServiceOperationStart>.Failure("Bu servis için süren bir işlem var; bitmesini bekleyin.", ServiceErrorType.Conflict);

            ServiceResult<ServiceOperationStart> result;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                result = await begin(scope.ServiceProvider.GetRequiredService<IManagedServiceService>(), cancellationToken);
            }

            if (!result.IsSuccess)
                return result;

            var run = new ServiceOperationRun(result.Data!.OperationId, result.Data.ServiceId, OutputBufferCapacity);
            _runs[run.Id] = run;
            run.Execution = Task.Run(() => ExecuteAsync(run, actor, onSucceeded), CancellationToken.None);
            return result;
        }
        finally
        {
            gate?.Release();
        }
    }

    /// <summary>Bellekte izlenen işlemin anlık görüntüsü; bu uygulama örneğinde çalışmıyorsa null.</summary>
    public ServiceOperationJoinResponse? Snapshot(Guid operationId) =>
        _runs.TryGetValue(operationId, out var run) ? run.Snapshot() : null;

    public bool IsRunning(Guid operationId) =>
        _runs.TryGetValue(operationId, out var run) && !run.IsCompleted;

    /// <summary>Uygulama kapanırken süren işlemleri durdurur; kayıtlar "kesildi" olarak işaretlenir.</summary>
    public async Task StopAllAsync()
    {
        if (_runs.IsEmpty)
            return;

        await _stopping.CancelAsync();
        var running = _runs.Values.Select(r => r.Execution).ToArray();
        await Task.WhenAny(Task.WhenAll(running), Task.Delay(ShutdownWait));
    }

    public void Dispose() => _stopping.Dispose();

    private async Task ExecuteAsync(ServiceOperationRun run, ServiceActor actor, Func<IServiceProvider, Guid, CancellationToken, Task<string?>>? onSucceeded)
    {
        var sink = new ServiceOperationSink(run, _hubContext, _logger);
        ServiceResult result;
        ManagedServiceOperationStatus? stored = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IManagedServiceService>();
            result = await service.RunOperationAsync(run.Id, actor, sink, _stopping.Token);
            stored = (await service.GetOperationAsync(run.Id, includeLog: false, CancellationToken.None)).Data?.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Servis işlemi çalıştırılamadı. OperationId: {OperationId}", run.Id);
            result = ServiceResult.Failure("İşlem beklenmeyen bir hata ile durdu.");
        }

        var status = stored is { } s && s != ManagedServiceOperationStatus.Running
            ? s
            : result.IsSuccess ? ManagedServiceOperationStatus.Succeeded : ManagedServiceOperationStatus.Failed;

        if (status == ManagedServiceOperationStatus.Succeeded && onSucceeded is not null)
            await RunFollowUpAsync(run, actor, sink, onSucceeded);

        run.Complete(status, result.Message);
        await sink.CompletedAsync(status, result.Message);
        _ = ForgetLaterAsync(run);
    }

    private async Task RunFollowUpAsync(
        ServiceOperationRun run,
        ServiceActor actor,
        ServiceOperationSink sink,
        Func<IServiceProvider, Guid, CancellationToken, Task<string?>> followUp)
    {
        string? message;
        try
        {
            using var user = CurrentUserService.RunAs(actor.UserId, actor.UserName, actor.IpAddress);
            await using var scope = _scopeFactory.CreateAsyncScope();
            message = await followUp(scope.ServiceProvider, run.ServiceId, _stopping.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Servis işlemi sonrası adım çalıştırılamadı. OperationId: {OperationId}", run.Id);
            message = ServiceConsole.Warning("İşlem sonrası adım tamamlanamadı; ayrıntı için uygulama loguna bakın.");
        }

        if (!string.IsNullOrEmpty(message))
            await sink.OnOutputAsync(message, CancellationToken.None);
    }

    private async Task ForgetLaterAsync(ServiceOperationRun run)
    {
        try
        {
            await Task.Delay(CompletedRetention, _stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _runs.TryRemove(run.Id, out _);
    }
}

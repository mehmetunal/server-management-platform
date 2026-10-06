using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Enums;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.Deployments;

/// <summary>
/// Deployment'ları HTTP isteğinden bağımsız arka planda çalıştırır. Tarayıcı kapansa da deployment sürer;
/// yeniden açılan sayfa <see cref="Snapshot"/> ile kaldığı yerden izler. Aynı projede aynı anda tek deployment çalışır.
/// </summary>
public sealed class DeploymentManager : IDisposable
{
    private const int OutputBufferCapacity = 512 * 1024;
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, DeploymentRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _projectGates = new();

    private readonly CancellationTokenSource _stopping = new();
    private readonly IHubContext<DeploymentHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeploymentManager> _logger;

    public DeploymentManager(IHubContext<DeploymentHub> hubContext, IServiceScopeFactory scopeFactory, ILogger<DeploymentManager> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<ServiceResult<Guid>> StartAsync(Guid projectId, StartDeploymentDto dto, DeploymentActor actor, CancellationToken cancellationToken) =>
        StartCoreAsync(projectId, actor, (service, ct) => service.BeginAsync(projectId, dto, actor, ct), cancellationToken);

    public async Task<ServiceResult<Guid>> RedeployAsync(Guid deploymentId, DeploymentActor actor, CancellationToken cancellationToken)
    {
        Guid projectId;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var source = await scope.ServiceProvider.GetRequiredService<IDeploymentService>().GetAsync(deploymentId, includeLog: false, cancellationToken);
            if (!source.IsSuccess)
                return ServiceResult<Guid>.NotFound(source.Message ?? "Deployment kaydı bulunamadı.");

            projectId = source.Data!.ProjectId;
        }

        return await StartCoreAsync(projectId, actor, (service, ct) => service.BeginRedeployAsync(deploymentId, actor, ct), cancellationToken);
    }

    public async Task<ServiceResult<Guid>> RollbackAsync(Guid deploymentId, DeploymentActor actor, CancellationToken cancellationToken)
    {
        Guid projectId;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var source = await scope.ServiceProvider.GetRequiredService<IDeploymentService>().GetAsync(deploymentId, includeLog: false, cancellationToken);
            if (!source.IsSuccess)
                return ServiceResult<Guid>.NotFound(source.Message ?? "Deployment kaydı bulunamadı.");

            projectId = source.Data!.ProjectId;
        }

        return await StartCoreAsync(projectId, actor, (service, ct) => service.BeginRollbackAsync(deploymentId, actor, ct), cancellationToken);
    }

    /// <summary>Build etmeden .env/override'ı yeniden yazar ve container'ları yeniden oluşturur (ortam değişkenlerini uygulamak için).</summary>
    public Task<ServiceResult<Guid>> RestartAsync(Guid projectId, DeploymentActor actor, CancellationToken cancellationToken) =>
        StartCoreAsync(projectId, actor, (service, ct) => service.BeginRestartAsync(projectId, actor, ct), cancellationToken);

    /// <summary>
    /// Webhook ile deploy. Projenin deployment'ı sürüyorsa yeni deployment başlatılmaz; <b>tek</b> bir takip deployment'ı
    /// kalıcı kuyruğa (proje kaydı) yazılır (sonraki push'lar aynı kaydı günceller) ve süren deployment bitince dalın son hali
    /// deploy edilir. Kuyruk veritabanında tutulduğu için uygulama yeniden başlasa da kaybolmaz; açılışta ve periyodik olarak
    /// <see cref="DrainPendingAsync"/> ile tüketilir.
    /// </summary>
    public async Task<WebhookDeployOutcome> StartFromWebhookAsync(Guid projectId, string? commit, DeploymentActor actor, CancellationToken cancellationToken)
    {
        var result = await StartAsync(projectId, new StartDeploymentDto { CommitSha = commit }, actor, cancellationToken);
        if (result.IsSuccess)
            return new WebhookDeployOutcome(WebhookDeployStatus.Started, result.Data, "Deploy başlatıldı.");

        if (result.ErrorType != ServiceErrorType.Conflict)
            return new WebhookDeployOutcome(WebhookDeployStatus.Failed, null, result.Message ?? "Deployment başlatılamadı.");

        PendingWebhookDeploy? pending;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            pending = await scope.ServiceProvider.GetRequiredService<IProjectWebhookService>()
                .QueueFollowUpAsync(projectId, commit, actor.IpAddress, cancellationToken);
        }

        if (pending is null)
            return new WebhookDeployOutcome(WebhookDeployStatus.Failed, null, "Proje bulunamadı.");

        // Süren deployment bu arada bittiyse kuyruğu tüketecek kimse kalmaz; hemen denenir. Hâlâ çakışıyorsa kayıt kuyrukta kalır.
        if (!HasActiveRun(projectId))
        {
            var drained = await TryStartPendingAsync(pending, cancellationToken);
            if (drained.Status == WebhookDeployStatus.Started)
                return new WebhookDeployOutcome(WebhookDeployStatus.Started, drained.DeploymentId, "Deploy başlatıldı.");
            if (drained.Status == WebhookDeployStatus.Failed)
                return drained;
        }

        return new WebhookDeployOutcome(WebhookDeployStatus.Queued, null, "Süren deployment bitince dalın son hali deploy edilecek (kuyrukta).");
    }

    /// <summary>
    /// Kalıcı kuyruktaki takip deploy'larını başlatmayı dener (açılışta ve periyodik). Bu örnekte deployment'ı süren projeler
    /// atlanır (deployment bitince kendisi tüketir); çakışma sürerse kayıt kuyrukta kalır.
    /// </summary>
    /// <returns>Başlatılan deployment sayısı.</returns>
    public async Task<int> DrainPendingAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingWebhookDeploy> pending;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            pending = await scope.ServiceProvider.GetRequiredService<IProjectWebhookService>().ListPendingFollowUpsAsync(cancellationToken);
        }

        var started = 0;
        foreach (var item in pending)
        {
            if (cancellationToken.IsCancellationRequested || HasActiveRun(item.ProjectId))
                continue;

            if (await StartFollowUpAsync(item, cancellationToken))
                started++;
        }

        return started;
    }

    private bool HasActiveRun(Guid projectId) => _runs.Values.Any(r => r.ProjectId == projectId && !r.IsCompleted);

    /// <summary>Bellekte izlenen deployment'ın anlık görüntüsü; bu uygulama örneğinde çalışmıyorsa null.</summary>
    public DeploymentJoinResponse? Snapshot(Guid deploymentId) =>
        _runs.TryGetValue(deploymentId, out var run) ? run.Snapshot() : null;

    public bool IsRunning(Guid deploymentId) =>
        _runs.TryGetValue(deploymentId, out var run) && !run.IsCompleted;

    public ServiceResult Cancel(Guid deploymentId, DeploymentActor actor)
    {
        if (!_runs.TryGetValue(deploymentId, out var run) || run.IsCompleted)
            return ServiceResult.Failure("Deployment çalışmıyor veya zaten bitti.", ServiceErrorType.Conflict);

        return run.Cancellation.Cancel(actor)
            ? ServiceResult.Success("İptal isteği gönderildi; çalışan komut durduruluyor.")
            : ServiceResult.Failure("İptal isteği zaten gönderildi.", ServiceErrorType.Conflict);
    }

    /// <summary>Uygulama kapanırken süren deployment'ları durdurur; kayıtlar "kesildi" olarak işaretlenir.</summary>
    public async Task StopAllAsync()
    {
        if (_runs.IsEmpty)
            return;

        await _stopping.CancelAsync();
        var running = _runs.Values.Select(r => r.Execution).ToArray();
        await Task.WhenAny(Task.WhenAll(running), Task.Delay(ShutdownWait));
    }

    public void Dispose() => _stopping.Dispose();

    private async Task<ServiceResult<Guid>> StartCoreAsync(
        Guid projectId,
        DeploymentActor actor,
        Func<IDeploymentService, CancellationToken, Task<ServiceResult<Guid>>> begin,
        CancellationToken cancellationToken)
    {
        var gate = _projectGates.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.Zero, cancellationToken))
            return ServiceResult<Guid>.Failure("Bu proje için deployment zaten başlatılıyor.", ServiceErrorType.Conflict);

        try
        {
            if (_runs.Values.Any(r => r.ProjectId == projectId && !r.IsCompleted))
                return ServiceResult<Guid>.Failure("Bu proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

            ServiceResult<Guid> result;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                result = await begin(scope.ServiceProvider.GetRequiredService<IDeploymentService>(), cancellationToken);
            }

            if (!result.IsSuccess)
                return result;

            var run = new DeploymentRun(result.Data, projectId, new DeploymentCancellation(_stopping.Token), OutputBufferCapacity);
            _runs[run.Id] = run;
            run.Execution = Task.Run(() => ExecuteAsync(run, actor), CancellationToken.None);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task ExecuteAsync(DeploymentRun run, DeploymentActor actor)
    {
        var sink = new DeploymentSink(run, _hubContext, _logger);
        ServiceResult result;
        DeploymentStatus? stored = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IDeploymentService>();
            result = await service.RunAsync(run.Id, actor, sink, run.Cancellation);
            stored = (await service.GetAsync(run.Id, includeLog: false, CancellationToken.None)).Data?.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment çalıştırılamadı. DeploymentId: {DeploymentId}", run.Id);
            result = ServiceResult.Failure("Deployment beklenmeyen bir hata ile durdu.");
        }

        var status = stored is { } s && !s.IsRunning()
            ? s
            : result.IsSuccess ? DeploymentStatus.Succeeded : DeploymentStatus.Failed;

        run.Complete(status, result.Message);
        await sink.CompletedAsync(status, result.Message);
        _ = ForgetLaterAsync(run);

        if (!_stopping.IsCancellationRequested)
            _ = DrainProjectAsync(run.ProjectId);
    }

    private async Task DrainProjectAsync(Guid projectId)
    {
        try
        {
            PendingWebhookDeploy? pending;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var all = await scope.ServiceProvider.GetRequiredService<IProjectWebhookService>().ListPendingFollowUpsAsync(_stopping.Token);
                pending = all.FirstOrDefault(p => p.ProjectId == projectId);
            }

            if (pending is not null)
                await StartFollowUpAsync(pending, _stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kuyruktaki webhook deploy'u okunamadı. ProjectId: {ProjectId}", projectId);
        }
    }

    /// <summary>Kuyruktaki deploy'u başlatır ve sonucu son teslimat durumu olarak yazar; çakışmada kayıt kuyrukta kalır.</summary>
    private async Task<bool> StartFollowUpAsync(PendingWebhookDeploy pending, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await TryStartPendingAsync(pending, cancellationToken);
            if (outcome.Status == WebhookDeployStatus.Queued)
                return false;

            var started = outcome.Status == WebhookDeployStatus.Started;
            var message = started
                ? "Kuyruktaki push deploy'u başlatıldı (dalın son hali)."
                : "Kuyruktaki push deploy'u başlatılamadı: " + outcome.Message;

            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IProjectWebhookService>().RecordDeliveryAsync(pending.ProjectId, started, message, cancellationToken);
            return started;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kuyruktaki webhook deploy'u başlatılamadı. ProjectId: {ProjectId}", pending.ProjectId);
            return false;
        }
    }

    /// <summary>
    /// Started: başlatıldı ve kuyruk kaydı tüketildi. Queued: çakışma sürüyor, kayıt kuyrukta. Failed: kalıcı hata, kayıt düşürüldü.
    /// </summary>
    private async Task<WebhookDeployOutcome> TryStartPendingAsync(PendingWebhookDeploy pending, CancellationToken cancellationToken)
    {
        var actor = new DeploymentActor(null, ProjectWebhooks.ActorName, pending.IpAddress);
        var started = await StartAsync(pending.ProjectId, new StartDeploymentDto(), actor, cancellationToken);
        if (!started.IsSuccess && started.ErrorType == ServiceErrorType.Conflict)
            return new WebhookDeployOutcome(WebhookDeployStatus.Queued, null, started.Message ?? "Süren deployment var.");

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProjectWebhookService>().CompleteFollowUpAsync(pending, CancellationToken.None);
        }

        return started.IsSuccess
            ? new WebhookDeployOutcome(WebhookDeployStatus.Started, started.Data, "Deploy başlatıldı.")
            : new WebhookDeployOutcome(WebhookDeployStatus.Failed, null, started.Message ?? "Deployment başlatılamadı.");
    }

    private async Task ForgetLaterAsync(DeploymentRun run)
    {
        try
        {
            await Task.Delay(CompletedRetention, _stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }

        _runs.TryRemove(run.Id, out _);
        run.Cancellation.Dispose();
    }
}

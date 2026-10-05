using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Terminal;
using ServerManager.Domain.Enums;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.Terminal;

/// <summary>
/// Açık SSH terminal oturumlarını tutar. Oturum tarayıcı bağlantısından bağımsızdır:
/// bağlantı koparsa belirli süre açık kalır ve aynı kullanıcı yeniden bağlanabilir.
/// </summary>
public sealed class TerminalManager
{
    private const string Cancel = "\u0003";
    private const string AccessRevokedMessage = "Oturum yetkiniz değişti veya sona erdi; terminal kapatıldı. Lütfen tekrar giriş yapın.";

    private readonly ConcurrentDictionary<Guid, TerminalRegistration> _sessions = new();
    private readonly Dictionary<string, int> _pendingByUser = new();
    private readonly Lock _gate = new();
    private readonly IHubContext<TerminalHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DangerousCommandDetector _detector;
    private readonly TerminalCommandQueue _commandQueue;
    private readonly TerminalOptions _options;
    private readonly TerminalAccessValidator _access;
    private readonly ILogger<TerminalManager> _logger;

    public TerminalManager(
        IHubContext<TerminalHub> hubContext,
        IServiceScopeFactory scopeFactory,
        DangerousCommandDetector detector,
        TerminalCommandQueue commandQueue,
        IOptions<TerminalOptions> options,
        TerminalAccessValidator access,
        ILogger<TerminalManager> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _detector = detector;
        _commandQueue = commandQueue;
        _options = options.Value;
        _access = access;
        _logger = logger;
    }

    public int ActiveCount => _sessions.Count;

    public Task<ServiceResult<Guid>> StartServerAsync(TerminalUser user, Guid serverId, int columns, int rows, CancellationToken cancellationToken) =>
        StartAsync(user, (services, actor, sink, ct) => services.GetRequiredService<ITerminalService>().OpenServerShellAsync(serverId, columns, rows, actor, sink, ct), cancellationToken);

    public Task<ServiceResult<Guid>> StartContainerAsync(TerminalUser user, Guid serverId, string container, int columns, int rows, CancellationToken cancellationToken) =>
        StartAsync(user, (services, actor, sink, ct) => services.GetRequiredService<ITerminalService>().OpenContainerShellAsync(serverId, container, columns, rows, actor, sink, ct), cancellationToken);

    /// <summary>
    /// Servisler › Konsol: servis container'ında şablonun istemci komutunu (psql, redis-cli …) açar. Komut sunucu tarafında
    /// şablondan seçilir; istemciden yalnızca servis kimliği alınır.
    /// </summary>
    public Task<ServiceResult<Guid>> StartServiceConsoleAsync(TerminalUser user, Guid serviceId, int columns, int rows, CancellationToken cancellationToken) =>
        StartAsync(user, async (services, actor, sink, ct) =>
        {
            var serviceActor = new ServiceActor(actor.UserId, actor.UserName, actor.IpAddress);
            var result = await services.GetRequiredService<IManagedServiceService>().OpenConsoleAsync(serviceId, columns, rows, serviceActor, sink, ct);
            if (result.IsSuccess)
                await services.GetRequiredService<ITerminalService>().RecordSessionAsync(result.Data!, actor, ct);
            return result;
        }, cancellationToken);

    public async Task<TerminalAttachResponse> AttachAsync(TerminalUser user, Guid sessionId, int columns, int rows)
    {
        if (!_sessions.TryGetValue(sessionId, out var registration) || registration.Actor.UserId != user.UserId || registration.Handle is null)
            return new TerminalAttachResponse(false, "Oturum bulunamadı veya sona erdi.");

        if (!TerminalAccessValidator.HasPermission(user.Principal, registration.Handle.Kind)
            || await _access.IsUserStillValidAsync(user.Principal) != true)
        {
            await CloseAsync(registration, AccessRevokedMessage);
            return new TerminalAttachResponse(false, AccessRevokedMessage);
        }

        var (previous, output) = registration.Attach(user.ConnectionId);
        if (previous is not null && previous != user.ConnectionId)
            await SendAsync(previous, TerminalHub.DetachedEvent, sessionId);

        registration.Handle.Session.Resize(columns, rows);

        var pending = registration.PendingConfirmation;
        return new TerminalAttachResponse(
            true,
            null,
            output,
            registration.Title,
            pending is null ? null : new TerminalConfirmationPayload(pending.Token, pending.Line.Text, pending.Description));
    }

    public void Detach(string connectionId)
    {
        var now = DateTime.UtcNow;
        foreach (var registration in _sessions.Values)
            registration.Detach(connectionId, now);
    }

    public async Task WriteAsync(string connectionId, Guid sessionId, string data, ClaimsPrincipal? principal)
    {
        if (!TryGetAttached(connectionId, sessionId, out var registration) || !await EnsureAccessAsync(registration, principal))
            return;

        registration.TouchInput();
        await registration.InputLock.WaitAsync();
        try
        {
            await ProcessInputAsync(registration, data);
        }
        finally
        {
            registration.InputLock.Release();
        }
    }

    public void Resize(string connectionId, Guid sessionId, int columns, int rows, ClaimsPrincipal? principal)
    {
        if (TryGetAttached(connectionId, sessionId, out var registration)
            && registration.Handle is { } handle
            && TerminalAccessValidator.HasPermission(principal, handle.Kind))
            handle.Session.Resize(columns, rows);
    }

    public async Task StopAsync(string connectionId, Guid sessionId)
    {
        if (TryGetAttached(connectionId, sessionId, out var registration))
            await CloseAsync(registration, "Oturum kullanıcı tarafından kapatıldı.");
    }

    public async Task ResolveConfirmationAsync(string connectionId, Guid sessionId, string token, bool approve, ClaimsPrincipal? principal)
    {
        if (!TryGetAttached(connectionId, sessionId, out var registration) || !await EnsureAccessAsync(registration, principal))
            return;

        registration.TouchInput();
        await registration.InputLock.WaitAsync();
        try
        {
            var pending = registration.PendingConfirmation;
            if (pending is null || !string.Equals(pending.Token, token, StringComparison.Ordinal))
                return;

            registration.PendingConfirmation = null;
            if (!approve)
            {
                await CancelPendingAsync(registration, pending, "Komut iptal edildi.");
                return;
            }

            await WriteToSessionAsync(registration, pending.Enter.ToString());
            Record(registration, pending.Line, TerminalCommandStatus.Confirmed, pending.Description);
            if (pending.Remainder.Length > 0)
                await ProcessInputAsync(registration, pending.Remainder);
        }
        finally
        {
            registration.InputLock.Release();
        }
    }

    public async Task<int> SweepAsync(DateTime now)
    {
        var idleThreshold = now - TimeSpan.FromMinutes(Math.Max(1, _options.IdleTimeoutMinutes));
        var detachedThreshold = now - TimeSpan.FromSeconds(Math.Max(10, _options.ReconnectGraceSeconds));
        var confirmationThreshold = now - TimeSpan.FromSeconds(Math.Max(10, _options.ConfirmationTimeoutSeconds));
        var closed = 0;

        foreach (var registration in _sessions.Values.ToList())
        {
            if (registration.Handle is null)
                continue;

            if (registration.LastInputAt < idleThreshold)
            {
                await NotifyAsync(registration, "warning", "Uzun süre işlem yapılmadığı için oturum kapatıldı.");
                await CloseAsync(registration, "Boşta kalma süresi doldu.");
                closed++;
                continue;
            }

            if (registration.DetachedAt is { } detachedAt && detachedAt < detachedThreshold)
            {
                await CloseAsync(registration, "Tarayıcı bağlantısı kapandı ve yeniden bağlanılmadı.");
                closed++;
                continue;
            }

            if (registration.PendingConfirmation is { } pending && pending.CreatedAt < confirmationThreshold)
                await ExpireConfirmationAsync(registration, pending.Token);
        }

        return closed;
    }

    public async Task CloseAllAsync(string reason)
    {
        foreach (var registration in _sessions.Values.ToList())
            await CloseAsync(registration, reason);
    }

    /// <summary>Kullanıcının tüm terminal oturumlarını kapatır (pasifleştirme, kilitleme, rol değişikliği, çıkış).</summary>
    public async Task<int> CloseForUserAsync(string userId, string reason)
    {
        _access.Invalidate(userId);

        var closed = 0;
        foreach (var registration in _sessions.Values.Where(r => r.Actor.UserId == userId).ToList())
        {
            await NotifyAsync(registration, "warning", reason);
            await CloseAsync(registration, reason);
            closed++;
        }

        if (closed > 0)
            _logger.LogInformation("Kullanıcının {Count} terminal oturumu kapatıldı. UserId: {UserId}, Reason: {Reason}", closed, userId, reason);

        return closed;
    }

    private async Task<ServiceResult<Guid>> StartAsync(
        TerminalUser user,
        Func<IServiceProvider, TerminalActor, TerminalSessionSink, CancellationToken, Task<ServiceResult<TerminalHandle>>> open,
        CancellationToken cancellationToken)
    {
        if (await _access.IsUserStillValidAsync(user.Principal, cancellationToken) != true)
            return ServiceResult<Guid>.Failure(AccessRevokedMessage, ServiceErrorType.Forbidden);

        var maxSessions = Math.Max(1, _options.MaxSessionsPerUser);
        if (!TryReserve(user.UserId, maxSessions))
        {
            return ServiceResult<Guid>.Failure(
                $"Aynı anda en fazla {maxSessions} terminal oturumu açabilirsiniz. Açık terminallerden birini kapatın.",
                ServiceErrorType.Conflict);
        }

        var actor = new TerminalActor(Guid.NewGuid(), user.UserId, user.UserName, user.IpAddress);
        var registration = new TerminalRegistration(actor, user.ConnectionId, Math.Max(16, _options.OutputBufferKilobytes) * 1024);
        var sink = new TerminalSessionSink(
            data => OnOutputAsync(registration, data),
            reason => CompleteAsync(registration, reason));

        ServiceResult<TerminalHandle> result;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            result = await open(scope.ServiceProvider, actor, sink, cancellationToken);
            if (result.IsSuccess)
                _sessions[actor.SessionId] = registration;
        }
        finally
        {
            lock (_gate)
                Release(user.UserId);
        }

        if (!result.IsSuccess)
            return ServiceResult<Guid>.Failure(result.Message ?? "Terminal açılamadı.", result.ErrorType);

        if (registration.SetHandle(result.Data!, out var closedReason))
            await CompleteAsync(registration, closedReason);

        return ServiceResult<Guid>.Success(actor.SessionId);
    }

    private async Task ProcessInputAsync(TerminalRegistration registration, string data)
    {
        // Onay beklerken yazılanlar kabuğa iletilmez; aksi halde bekletilen komutun önüne geçer.
        if (registration.PendingConfirmation is not null || registration.Handle is null)
            return;

        if (registration.Monitor.IsAlternateScreen)
        {
            registration.Tracker.Reset();
            await WriteToSessionAsync(registration, data);
            return;
        }

        var forward = new StringBuilder();
        var rest = data.AsMemory();
        while (rest.Length > 0)
        {
            var step = registration.Tracker.Next(rest.Span);
            var chunk = rest[..step.Consumed];
            rest = rest[step.Consumed..];

            if (step.Submitted is not { } line)
            {
                forward.Append(chunk.Span);
                continue;
            }

            forward.Append(chunk.Span[..^1]);
            var enter = chunk.Span[^1];

            if (registration.Monitor.IsSecretPrompt)
            {
                forward.Append(enter);
                continue;
            }

            var match = _detector.Detect(line.Text);
            if (match is null || match.Mode == DangerousCommandMode.Warn)
            {
                forward.Append(enter);
                Record(registration, line, TerminalCommandStatus.Executed, match?.Description);
                if (match is not null)
                    await NotifyAsync(registration, "warning", $"Tehlikeli komut çalıştırıldı: {match.Description}. Audit log'a yazıldı.");
                continue;
            }

            await WriteToSessionAsync(registration, forward.ToString());
            forward.Clear();

            if (match.Mode == DangerousCommandMode.Block)
            {
                await WriteToSessionAsync(registration, Cancel);
                Record(registration, line, TerminalCommandStatus.Blocked, match.Description);
                await NotifyAsync(registration, "error", $"Komut engellendi: {match.Description}. Bu komut terminal politikası gereği çalıştırılamaz.");
                return;
            }

            var pending = new PendingTerminalConfirmation(
                Convert.ToHexString(RandomNumberGenerator.GetBytes(12)),
                line,
                enter,
                rest.ToString(),
                match.Description,
                DateTime.UtcNow);
            registration.PendingConfirmation = pending;
            await SendToClientAsync(registration, TerminalHub.ConfirmEvent, new TerminalConfirmationPayload(pending.Token, line.Text, match.Description));
            return;
        }

        await WriteToSessionAsync(registration, forward.ToString());
    }

    private async Task ExpireConfirmationAsync(TerminalRegistration registration, string token)
    {
        await registration.InputLock.WaitAsync();
        try
        {
            if (registration.PendingConfirmation is not { } pending || pending.Token != token)
                return;

            registration.PendingConfirmation = null;
            await CancelPendingAsync(registration, pending, "Onay süresi dolduğu için komut iptal edildi.");
        }
        finally
        {
            registration.InputLock.Release();
        }
    }

    private async Task CancelPendingAsync(TerminalRegistration registration, PendingTerminalConfirmation pending, string message)
    {
        await WriteToSessionAsync(registration, Cancel);
        registration.Tracker.Reset();
        Record(registration, pending.Line, TerminalCommandStatus.Cancelled, pending.Description);
        await NotifyAsync(registration, "info", message);
    }

    private void Record(TerminalRegistration registration, TerminalCommandLine line, TerminalCommandStatus status, string? matchedRule)
    {
        var text = line.Text.Trim();
        if (text.Length == 0 && !line.IsApproximate)
            return;

        var handle = registration.Handle!;
        var entry = new TerminalCommandEntry(
            registration.SessionId,
            handle.ServerId,
            handle.ServerName,
            registration.Actor,
            DateTime.UtcNow,
            text,
            line.IsApproximate,
            status,
            matchedRule);

        if (!_commandQueue.TryEnqueue(entry))
            _logger.LogWarning("Terminal komut kuyruğu dolu; komut kaydı atlandı. SessionId: {SessionId}", registration.SessionId);

        if (text.Length > 0 && !line.IsApproximate && status is TerminalCommandStatus.Executed or TerminalCommandStatus.Confirmed)
            _ = SendToClientAsync(registration, TerminalHub.CommandEvent, text);
    }

    private async Task OnOutputAsync(TerminalRegistration registration, string data)
    {
        registration.Monitor.Inspect(data);
        var connectionId = registration.AppendOutput(data);
        if (connectionId is not null)
            await SendAsync(connectionId, TerminalHub.OutputEvent, registration.SessionId, data);
    }

    private async Task WriteToSessionAsync(TerminalRegistration registration, string data)
    {
        if (data.Length > 0 && registration.Handle is { } handle)
            await handle.Session.WriteAsync(data);
    }

    private Task NotifyAsync(TerminalRegistration registration, string level, string message) =>
        SendToClientAsync(registration, TerminalHub.NoticeEvent, new TerminalNoticePayload(level, message));

    private Task SendToClientAsync(TerminalRegistration registration, string method, object payload)
    {
        var connectionId = registration.ConnectionId;
        return connectionId is null ? Task.CompletedTask : SendAsync(connectionId, method, registration.SessionId, payload);
    }

    private async Task SendAsync(string connectionId, string method, params object?[] args)
    {
        try
        {
            await _hubContext.Clients.Client(connectionId).SendCoreAsync(method, args);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Terminal olayı istemciye gönderilemedi. Method: {Method}", method);
        }
    }

    /// <summary>
    /// Bağlantının kimliği hub açıldığı andaki haliyle kalır; her girdide yetki ve kullanıcı durumu yeniden kontrol edilir.
    /// Veritabanına geçici olarak ulaşılamazsa (null) oturum kesilmez.
    /// </summary>
    private async Task<bool> EnsureAccessAsync(TerminalRegistration registration, ClaimsPrincipal? principal)
    {
        var kind = registration.Handle?.Kind ?? TerminalSessionKind.Server;
        if (TerminalAccessValidator.HasPermission(principal, kind)
            && principal?.FindFirstValue(ClaimTypes.NameIdentifier) == registration.Actor.UserId
            && await _access.IsUserStillValidAsync(principal) != false)
            return true;

        await NotifyAsync(registration, "error", AccessRevokedMessage);
        await CloseAsync(registration, AccessRevokedMessage);
        return false;
    }

    private bool TryGetAttached(string connectionId, Guid sessionId, out TerminalRegistration registration)
    {
        if (_sessions.TryGetValue(sessionId, out registration!) && registration.ConnectionId == connectionId)
            return true;

        registration = null!;
        return false;
    }

    private async Task CloseAsync(TerminalRegistration registration, string reason)
    {
        try
        {
            if (registration.Handle is { } handle)
                await handle.Session.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal oturumu kapatılırken hata oluştu. Reason: {Reason}", reason);
        }

        await CompleteAsync(registration, reason);
    }

    private async Task CompleteAsync(TerminalRegistration registration, string? reason)
    {
        if (!registration.TryGetHandleForClose(reason, out var handle) || !registration.TryMarkCompleted())
            return;

        _sessions.TryRemove(registration.SessionId, out _);

        var connectionId = registration.ConnectionId;
        if (connectionId is not null)
            await SendAsync(connectionId, TerminalHub.ClosedEvent, registration.SessionId, reason);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITerminalService>();
            await service.CompleteSessionAsync(handle!, registration.Actor, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal kapanış kaydı yazılamadı. ServerId: {ServerId}", handle!.ServerId);
        }
    }

    private bool TryReserve(string userId, int maxSessions)
    {
        lock (_gate)
        {
            var active = _sessions.Values.Count(r => r.Actor.UserId == userId);
            var pending = _pendingByUser.GetValueOrDefault(userId);
            if (active + pending >= maxSessions)
                return false;

            _pendingByUser[userId] = pending + 1;
            return true;
        }
    }

    private void Release(string userId)
    {
        var pending = _pendingByUser.GetValueOrDefault(userId) - 1;
        if (pending <= 0)
            _pendingByUser.Remove(userId);
        else
            _pendingByUser[userId] = pending;
    }
}

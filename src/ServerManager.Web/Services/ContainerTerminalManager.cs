using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.Services;

public sealed class ContainerTerminalManager
{
    private readonly ConcurrentDictionary<string, ContainerTerminalRegistration> _sessions = new();
    private readonly Dictionary<string, int> _pendingByUser = new();
    private readonly Lock _gate = new();
    private readonly IHubContext<ContainerTerminalHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DockerOptions _options;
    private readonly ILogger<ContainerTerminalManager> _logger;

    public ContainerTerminalManager(
        IHubContext<ContainerTerminalHub> hubContext,
        IServiceScopeFactory scopeFactory,
        IOptions<DockerOptions> options,
        ILogger<ContainerTerminalManager> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public int ActiveCount => _sessions.Count;

    public async Task<ServiceResult> StartAsync(
        string connectionId,
        string userId,
        string? userName,
        IDockerService dockerService,
        Guid serverId,
        string container,
        int columns,
        int rows,
        CancellationToken cancellationToken)
    {
        await StopAsync(connectionId, "Yeni terminal oturumu açıldı.");

        var maxSessions = Math.Max(1, _options.MaxTerminalSessionsPerUser);
        if (!TryReserve(userId, maxSessions))
            return ServiceResult.Failure($"Aynı anda en fazla {maxSessions} terminal oturumu açabilirsiniz. Açık terminallerden birini kapatın.", ServiceErrorType.Conflict);

        var sessionId = Guid.NewGuid();
        var sink = new SignalRTerminalOutputSink(
            _hubContext.Clients.Client(connectionId),
            _ => CompleteAsync(connectionId, sessionId));

        ContainerTerminalRegistration? registration = null;
        try
        {
            var result = await dockerService.OpenTerminalAsync(serverId, container, columns, rows, sink, cancellationToken);
            if (!result.IsSuccess)
                return ServiceResult.Failure(result.Message ?? "Terminal açılamadı.", result.ErrorType);

            registration = new ContainerTerminalRegistration(sessionId, connectionId, userId, userName, result.Data!);
        }
        finally
        {
            lock (_gate)
            {
                Release(userId);
                if (registration is not null)
                    _sessions[connectionId] = registration;
            }
        }

        if (registration.Handle.Session.IsClosed)
            await CompleteAsync(connectionId, sessionId);

        return ServiceResult.Success();
    }

    public async Task WriteAsync(string connectionId, string data)
    {
        if (!_sessions.TryGetValue(connectionId, out var registration))
            return;

        registration.TouchInput();
        await registration.Handle.Session.WriteAsync(data);
    }

    public void Resize(string connectionId, int columns, int rows)
    {
        if (_sessions.TryGetValue(connectionId, out var registration))
            registration.Handle.Session.Resize(columns, rows);
    }

    public async Task StopAsync(string connectionId, string reason)
    {
        if (!_sessions.TryGetValue(connectionId, out var registration))
            return;

        await CloseAsync(registration, reason);
    }

    public async Task<int> CloseIdleAsync(TimeSpan idleTimeout)
    {
        var threshold = DateTime.UtcNow - idleTimeout;
        var idle = _sessions.Values.Where(r => r.LastInputAt < threshold).ToList();

        foreach (var registration in idle)
        {
            await _hubContext.Clients.Client(registration.ConnectionId)
                .SendAsync(ContainerTerminalHub.OutputEvent, "\r\n\u001b[33mUzun süre işlem yapılmadığı için oturum kapatıldı.\u001b[0m\r\n");
            await CloseAsync(registration, "Boşta kalma süresi doldu.");
        }

        return idle.Count;
    }

    public async Task CloseAllAsync(string reason)
    {
        foreach (var registration in _sessions.Values.ToList())
            await CloseAsync(registration, reason);
    }

    private async Task CloseAsync(ContainerTerminalRegistration registration, string reason)
    {
        try
        {
            await registration.Handle.Session.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal oturumu kapatılırken hata oluştu. Reason: {Reason}", reason);
        }

        await CompleteAsync(registration.ConnectionId, registration.SessionId);
    }

    private async Task CompleteAsync(string connectionId, Guid sessionId)
    {
        if (!_sessions.TryGetValue(connectionId, out var registration)
            || registration.SessionId != sessionId
            || !_sessions.TryRemove(KeyValuePair.Create(connectionId, registration)))
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dockerService = scope.ServiceProvider.GetRequiredService<IDockerService>();
            await dockerService.LogTerminalClosedAsync(registration.Handle, registration.UserName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal kapanış kaydı yazılamadı. ServerId: {ServerId}", registration.Handle.ServerId);
        }
    }

    private bool TryReserve(string userId, int maxSessions)
    {
        lock (_gate)
        {
            var active = _sessions.Values.Count(r => r.UserId == userId);
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

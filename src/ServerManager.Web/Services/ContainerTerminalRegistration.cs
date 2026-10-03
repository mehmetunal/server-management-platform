using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Web.Services;

public sealed class ContainerTerminalRegistration
{
    private long _lastInputTicks;

    public ContainerTerminalRegistration(Guid sessionId, string connectionId, string userId, string? userName, ContainerTerminalHandle handle)
    {
        SessionId = sessionId;
        ConnectionId = connectionId;
        UserId = userId;
        UserName = userName;
        Handle = handle;
        _lastInputTicks = DateTime.UtcNow.Ticks;
    }

    public Guid SessionId { get; }

    public string ConnectionId { get; }

    public string UserId { get; }

    public string? UserName { get; }

    public ContainerTerminalHandle Handle { get; }

    public DateTime LastInputAt => new(Interlocked.Read(ref _lastInputTicks), DateTimeKind.Utc);

    public void TouchInput() => Interlocked.Exchange(ref _lastInputTicks, DateTime.UtcNow.Ticks);
}

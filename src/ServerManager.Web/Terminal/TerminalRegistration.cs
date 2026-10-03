using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Terminal;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Terminal;

public sealed class TerminalRegistration
{
    private readonly Lock _gate = new();
    private string? _connectionId;
    private DateTime? _detachedAt;
    private long _lastInputTicks;
    private int _completed;
    private TerminalHandle? _handle;
    private string? _closedDuringOpenReason;
    private bool _closedDuringOpen;

    public TerminalRegistration(TerminalActor actor, string connectionId, int outputBufferCapacity)
    {
        Actor = actor;
        _connectionId = connectionId;
        Buffer = new TerminalOutputBuffer(outputBufferCapacity);
        _lastInputTicks = DateTime.UtcNow.Ticks;
    }

    public TerminalActor Actor { get; }

    public Guid SessionId => Actor.SessionId;

    public TerminalOutputBuffer Buffer { get; }

    public TerminalOutputMonitor Monitor { get; } = new();

    public TerminalInputTracker Tracker { get; } = new();

    /// <summary>Girdi işleme ve onay akışını sıralar; tracker ve bekleyen onay yalnızca bu kilit altında değişir.</summary>
    public SemaphoreSlim InputLock { get; } = new(1, 1);

    public PendingTerminalConfirmation? PendingConfirmation { get; set; }

    public TerminalHandle? Handle
    {
        get
        {
            lock (_gate)
                return _handle;
        }
    }

    public string Title => Handle is { Kind: TerminalSessionKind.Container } handle
        ? $"{handle.Container} (container)"
        : Handle?.ServerName ?? "Terminal";

    public string? ConnectionId
    {
        get
        {
            lock (_gate)
                return _connectionId;
        }
    }

    public DateTime? DetachedAt
    {
        get
        {
            lock (_gate)
                return _detachedAt;
        }
    }

    public DateTime LastInputAt => new(Interlocked.Read(ref _lastInputTicks), DateTimeKind.Utc);

    public bool IsCompleted => Volatile.Read(ref _completed) == 1;

    public void TouchInput() => Interlocked.Exchange(ref _lastInputTicks, DateTime.UtcNow.Ticks);

    /// <returns>Oturum açılırken kapandıysa kapanma nedeni ile true.</returns>
    public bool SetHandle(TerminalHandle handle, out string? closedReason)
    {
        lock (_gate)
        {
            _handle = handle;
            closedReason = _closedDuringOpenReason;
            return _closedDuringOpen;
        }
    }

    /// <returns>Handle henüz atanmadıysa false; kapanış SetHandle sonrasına ertelenir.</returns>
    public bool TryGetHandleForClose(string? reason, out TerminalHandle? handle)
    {
        lock (_gate)
        {
            handle = _handle;
            if (handle is not null)
                return true;

            _closedDuringOpen = true;
            _closedDuringOpenReason = reason;
            return false;
        }
    }

    public bool TryMarkCompleted() => Interlocked.Exchange(ref _completed, 1) == 0;

    /// <summary>Çıktıyı tampona ekler ve o an bağlı istemciyi döner. Tampon ile bağlantı birlikte değiştiği için yeniden bağlanmada çıktı kaybolmaz.</summary>
    public string? AppendOutput(string data)
    {
        lock (_gate)
        {
            Buffer.Append(data);
            return _connectionId;
        }
    }

    /// <returns>Önceki bağlantı ve o ana kadarki ekran çıktısı.</returns>
    public (string? PreviousConnectionId, string Output) Attach(string connectionId)
    {
        lock (_gate)
        {
            var previous = _connectionId;
            _connectionId = connectionId;
            _detachedAt = null;
            return (previous, Buffer.Snapshot());
        }
    }

    public bool Detach(string connectionId, DateTime now)
    {
        lock (_gate)
        {
            if (_connectionId != connectionId)
                return false;

            _connectionId = null;
            _detachedAt = now;
            return true;
        }
    }
}

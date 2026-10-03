using ServerManager.Application.Dokploy;
using ServerManager.Application.Terminal;

namespace ServerManager.Web.Dokploy;

/// <summary>Bellekte izlenen kurulum: sonradan bağlanan tarayıcıya ekranı geri yüklemek için çıktı ve aşama tutulur.</summary>
public sealed class DokployInstallationRun
{
    private readonly Lock _gate = new();
    private readonly TerminalOutputBuffer _output;
    private long _sequence;
    private DokployInstallStage _stage = DokployInstallStage.Checking;
    private string? _stageMessage;
    private bool _completed;
    private bool _succeeded;
    private string? _message;

    public DokployInstallationRun(Guid id, Guid serverId, int bufferCapacity)
    {
        Id = id;
        ServerId = serverId;
        _output = new TerminalOutputBuffer(bufferCapacity);
    }

    public Guid Id { get; }

    public Guid ServerId { get; }

    public Task Execution { get; set; } = Task.CompletedTask;

    public bool IsCompleted
    {
        get
        {
            lock (_gate)
                return _completed;
        }
    }

    public long AppendOutput(string text)
    {
        lock (_gate)
        {
            _output.Append(text);
            return ++_sequence;
        }
    }

    public void SetStage(DokployInstallStage stage, string message)
    {
        lock (_gate)
        {
            _stage = stage;
            _stageMessage = message;
        }
    }

    public void Complete(bool succeeded, string? message)
    {
        lock (_gate)
        {
            _completed = true;
            _succeeded = succeeded;
            _message = message;
        }
    }

    public DokployJoinResponse Snapshot()
    {
        lock (_gate)
        {
            return new DokployJoinResponse(
                true,
                _completed ? _message : null,
                _output.Snapshot(),
                _sequence,
                _stage.ToString(),
                _stageMessage,
                _completed,
                _succeeded);
        }
    }
}

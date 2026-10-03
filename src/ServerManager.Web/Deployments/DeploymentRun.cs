using ServerManager.Application.Deployments;
using ServerManager.Application.Terminal;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Deployments;

/// <summary>Bellekte izlenen deployment: sonradan bağlanan tarayıcıya ekranı geri yüklemek için çıktı ve aşama tutulur.</summary>
public sealed class DeploymentRun
{
    private readonly Lock _gate = new();
    private readonly TerminalOutputBuffer _output;
    private long _sequence;
    private DeploymentStage _stage = DeploymentStage.Preparing;
    private DeploymentStatus? _status;
    private string? _message;

    public DeploymentRun(Guid id, Guid projectId, DeploymentCancellation cancellation, int bufferCapacity)
    {
        Id = id;
        ProjectId = projectId;
        Cancellation = cancellation;
        _output = new TerminalOutputBuffer(bufferCapacity);
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public DeploymentCancellation Cancellation { get; }

    public Task Execution { get; set; } = Task.CompletedTask;

    public bool IsCompleted
    {
        get
        {
            lock (_gate)
                return _status is not null;
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

    /// <summary>Başarısız/iptal aşamaları ulaşılan aşamayı değiştirmez; istemci hangi adımda durulduğunu buradan bilir.</summary>
    public void SetStage(DeploymentStage stage)
    {
        if (stage is DeploymentStage.Failed or DeploymentStage.Cancelled)
            return;

        lock (_gate)
            _stage = stage;
    }

    public void Complete(DeploymentStatus status, string? message)
    {
        lock (_gate)
        {
            _status = status;
            _message = message;
        }
    }

    public DeploymentJoinResponse Snapshot()
    {
        lock (_gate)
        {
            return new DeploymentJoinResponse(
                true,
                _message,
                _output.Snapshot(),
                _sequence,
                _stage.ToString(),
                _status is not null,
                _status?.ToString());
        }
    }
}

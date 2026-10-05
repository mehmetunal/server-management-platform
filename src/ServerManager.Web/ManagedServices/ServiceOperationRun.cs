using ServerManager.Application.ManagedServices;
using ServerManager.Application.Terminal;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.ManagedServices;

/// <param name="Sequence">Çıktının hangi parçaya kadar <paramref name="Output"/> içinde olduğu; istemci bu numaraya kadar gelen canlı parçaları yok sayar.</param>
/// <param name="Stage">Ulaşılan son aşama (<see cref="ServiceOperationStage"/> adı).</param>
/// <param name="Status">Bittiyse sonuç (<see cref="ManagedServiceOperationStatus"/> adı).</param>
public sealed record ServiceOperationJoinResponse(
    bool IsSuccess,
    string? Message,
    string Output = "",
    long Sequence = 0,
    string? Stage = null,
    bool IsCompleted = false,
    string? Status = null);

/// <summary>Bellekte izlenen servis işlemi: sonradan bağlanan tarayıcıya ekranı geri yüklemek için çıktı ve aşama tutulur.</summary>
public sealed class ServiceOperationRun
{
    private readonly Lock _gate = new();
    private readonly TerminalOutputBuffer _output;
    private long _sequence;
    private ServiceOperationStage _stage = ServiceOperationStage.Docker;
    private ManagedServiceOperationStatus? _status;
    private string? _message;

    public ServiceOperationRun(Guid id, Guid serviceId, int bufferCapacity)
    {
        Id = id;
        ServiceId = serviceId;
        _output = new TerminalOutputBuffer(bufferCapacity);
    }

    public Guid Id { get; }

    public Guid ServiceId { get; }

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

    /// <summary>Başarısız aşaması ulaşılan aşamayı değiştirmez; istemci hangi adımda durulduğunu buradan bilir.</summary>
    public void SetStage(ServiceOperationStage stage)
    {
        if (stage == ServiceOperationStage.Failed)
            return;

        lock (_gate)
            _stage = stage;
    }

    public void Complete(ManagedServiceOperationStatus status, string? message)
    {
        lock (_gate)
        {
            _status = status;
            _message = message;
        }
    }

    public ServiceOperationJoinResponse Snapshot()
    {
        lock (_gate)
        {
            return new ServiceOperationJoinResponse(
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

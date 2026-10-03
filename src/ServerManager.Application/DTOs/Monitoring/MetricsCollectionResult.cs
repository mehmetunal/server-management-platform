namespace ServerManager.Application.DTOs.Monitoring;

public sealed class MetricsCollectionResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool FingerprintMismatch { get; init; }

    public long DurationMs { get; init; }

    public SystemMetricsSnapshot? Snapshot { get; init; }
}

namespace ServerManager.Application.DTOs.Monitoring;

public sealed class MetricPointDto
{
    public DateTime Timestamp { get; init; }

    public double CpuUsagePercent { get; init; }

    public double MemoryUsagePercent { get; init; }

    public double DiskUsagePercent { get; init; }

    public double NetworkRxBytesPerSecond { get; init; }

    public double NetworkTxBytesPerSecond { get; init; }

    public double LoadAverage1 { get; init; }
}

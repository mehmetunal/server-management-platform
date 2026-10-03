using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Monitoring;

public sealed class ServerResourceSummaryDto
{
    public Guid ServerId { get; init; }

    public DateTime CollectedAt { get; init; }

    public double CpuUsagePercent { get; init; }

    public double MemoryUsagePercent { get; init; }

    public double DiskUsagePercent { get; init; }

    public double LoadAverage1 { get; init; }

    public double NetworkRxBytesPerSecond { get; init; }

    public double NetworkTxBytesPerSecond { get; init; }

    public long UptimeSeconds { get; init; }

    public ServerStatus? Status { get; init; }
}

namespace ServerManager.Application.DTOs.Monitoring;

public sealed class FleetResourceSummaryDto
{
    public int ReportingServers { get; init; }

    public double? AverageCpuUsagePercent { get; init; }

    public double? AverageMemoryUsagePercent { get; init; }

    public double? AverageDiskUsagePercent { get; init; }

    public double TotalNetworkRxBytesPerSecond { get; init; }

    public double TotalNetworkTxBytesPerSecond { get; init; }
}

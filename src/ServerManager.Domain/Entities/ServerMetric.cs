namespace ServerManager.Domain.Entities;

public class ServerMetric
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    public double CpuUsagePercent { get; set; }

    public double LoadAverage1 { get; set; }

    public double LoadAverage5 { get; set; }

    public double LoadAverage15 { get; set; }

    public long MemoryTotalBytes { get; set; }

    public long MemoryUsedBytes { get; set; }

    public double MemoryUsagePercent { get; set; }

    public long SwapTotalBytes { get; set; }

    public long SwapUsedBytes { get; set; }

    public long DiskTotalBytes { get; set; }

    public long DiskUsedBytes { get; set; }

    public double DiskUsagePercent { get; set; }

    public double NetworkRxBytesPerSecond { get; set; }

    public double NetworkTxBytesPerSecond { get; set; }

    public long UptimeSeconds { get; set; }
}

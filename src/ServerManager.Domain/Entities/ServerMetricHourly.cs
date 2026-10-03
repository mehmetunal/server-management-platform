namespace ServerManager.Domain.Entities;

public class ServerMetricHourly
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTime HourStart { get; set; }

    public int SampleCount { get; set; }

    public double CpuUsagePercentAvg { get; set; }

    public double CpuUsagePercentMax { get; set; }

    public double MemoryUsagePercentAvg { get; set; }

    public double MemoryUsagePercentMax { get; set; }

    public double DiskUsagePercentAvg { get; set; }

    public double DiskUsagePercentMax { get; set; }

    public double NetworkRxBytesPerSecondAvg { get; set; }

    public double NetworkTxBytesPerSecondAvg { get; set; }

    public double LoadAverage1Avg { get; set; }
}

namespace ServerManager.Domain.Entities;

/// <summary>Container kullanımının saatlik özeti (ham örnekler kısa tutulur, özetler uzun saklanır).</summary>
public class ContainerMetricHourly
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public string ContainerName { get; set; } = string.Empty;

    public DateTime HourStart { get; set; }

    public int SampleCount { get; set; }

    public double CpuPercentAvg { get; set; }

    public double CpuPercentMax { get; set; }

    public double MemoryUsageBytesAvg { get; set; }

    public long MemoryUsageBytesMax { get; set; }

    public int RestartCountMax { get; set; }
}

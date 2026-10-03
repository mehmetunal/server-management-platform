namespace ServerManager.Application.DTOs.Monitoring;

public sealed class ProcessUsageInfo
{
    public int Pid { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? User { get; set; }

    public double CpuPercent { get; set; }

    public double MemoryPercent { get; set; }
}

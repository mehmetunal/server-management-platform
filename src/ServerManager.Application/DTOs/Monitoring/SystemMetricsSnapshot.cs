namespace ServerManager.Application.DTOs.Monitoring;

public sealed class SystemMetricsSnapshot
{
    public double CpuUsagePercent { get; set; }

    public int CpuCores { get; set; }

    public int CpuThreads { get; set; }

    public double LoadAverage1 { get; set; }

    public double LoadAverage5 { get; set; }

    public double LoadAverage15 { get; set; }

    public double? CpuTemperatureCelsius { get; set; }

    public long MemoryTotalBytes { get; set; }

    public long MemoryFreeBytes { get; set; }

    public long MemoryAvailableBytes { get; set; }

    public long MemoryCachedBytes { get; set; }

    public long MemoryUsedBytes => Math.Max(0, MemoryTotalBytes - MemoryAvailableBytes);

    public double MemoryUsagePercent => Percent(MemoryUsedBytes, MemoryTotalBytes);

    public long SwapTotalBytes { get; set; }

    public long SwapFreeBytes { get; set; }

    public long SwapUsedBytes => Math.Max(0, SwapTotalBytes - SwapFreeBytes);

    public List<DiskUsageInfo> Disks { get; set; } = [];

    public List<NetworkInterfaceInfo> NetworkInterfaces { get; set; } = [];

    public List<ProcessUsageInfo> TopProcesses { get; set; } = [];

    public SystemInfo System { get; set; } = new();

    public static double Percent(long used, long total) =>
        total <= 0 ? 0 : Math.Round(used * 100d / total, 1);
}

namespace ServerManager.Application.DTOs.Monitoring;

public sealed class DiskUsageInfo
{
    public string FileSystem { get; set; } = string.Empty;

    public string MountPoint { get; set; } = string.Empty;

    public long TotalBytes { get; set; }

    public long UsedBytes { get; set; }

    public long AvailableBytes { get; set; }

    public double UsagePercent { get; set; }

    public double? InodeUsagePercent { get; set; }
}

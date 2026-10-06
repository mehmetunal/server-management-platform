using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Monitoring;

public class MonitoringMappingsTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Disk_sizes_come_from_the_fullest_partition_like_the_percent()
    {
        var snapshot = new SystemMetricsSnapshot
        {
            Disks =
            [
                new DiskUsageInfo { MountPoint = "/", TotalBytes = 100, UsedBytes = 40, UsagePercent = 40 },
                new DiskUsageInfo { MountPoint = "/data", TotalBytes = 50, UsedBytes = 45, UsagePercent = 90 }
            ]
        };

        var metric = snapshot.ToMetric(Guid.NewGuid(), Now);

        Assert.Equal(90, metric.DiskUsagePercent);
        Assert.Equal(45, metric.DiskUsedBytes);
        Assert.Equal(50, metric.DiskTotalBytes);
    }

    [Fact]
    public void Disk_sizes_are_zero_without_disks()
    {
        var metric = new SystemMetricsSnapshot().ToMetric(Guid.NewGuid(), Now);

        Assert.Equal(0, metric.DiskUsedBytes);
        Assert.Equal(0, metric.DiskTotalBytes);
    }

    [Fact]
    public void Summary_carries_byte_values_and_cpu_threads()
    {
        var snapshot = new SystemMetricsSnapshot
        {
            CpuUsagePercent = 40,
            MemoryTotalBytes = 8_000,
            MemoryAvailableBytes = 6_000,
            Disks = [new DiskUsageInfo { MountPoint = "/", TotalBytes = 100, UsedBytes = 25, UsagePercent = 25 }]
        };

        var summary = snapshot.ToMetric(Guid.NewGuid(), Now).ToSummaryDto(ServerStatus.Healthy, cpuThreads: 4);

        Assert.Equal(4, summary.CpuThreads);
        Assert.Equal(2_000, summary.MemoryUsedBytes);
        Assert.Equal(8_000, summary.MemoryTotalBytes);
        Assert.Equal(25, summary.DiskUsedBytes);
        Assert.Equal(100, summary.DiskTotalBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Unknown_cpu_threads_stay_null(int? threads)
    {
        var summary = new SystemMetricsSnapshot().ToMetric(Guid.NewGuid(), Now).ToSummaryDto(null, threads);

        Assert.Null(summary.CpuThreads);
    }
}

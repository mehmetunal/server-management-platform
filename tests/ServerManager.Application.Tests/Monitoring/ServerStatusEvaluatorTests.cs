using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Monitoring;

public class ServerStatusEvaluatorTests
{
    private static readonly MonitoringOptions Options = new();

    private static SystemMetricsSnapshot Snapshot(double cpu, double memoryPercent, params double[] diskPercents) => new()
    {
        CpuUsagePercent = cpu,
        MemoryTotalBytes = 1000,
        MemoryAvailableBytes = (long)(1000 - memoryPercent * 10),
        Disks = diskPercents.Select((p, i) => new DiskUsageInfo { MountPoint = i == 0 ? "/" : $"/disk{i}", UsagePercent = p }).ToList()
    };

    [Fact]
    public void Healthy_when_all_metrics_below_warning()
    {
        var result = ServerStatusEvaluator.Evaluate(Snapshot(50, 60, 40), Options);

        Assert.Equal(ServerStatus.Healthy, result.Status);
        Assert.Empty(result.Reasons);
    }

    [Theory]
    [InlineData(80, 10, 10)]
    [InlineData(10, 85, 10)]
    [InlineData(10, 10, 80)]
    public void Warning_at_warning_thresholds(double cpu, double memory, double disk)
    {
        var result = ServerStatusEvaluator.Evaluate(Snapshot(cpu, memory, disk), Options);

        Assert.Equal(ServerStatus.Warning, result.Status);
        Assert.Single(result.Reasons);
    }

    [Theory]
    [InlineData(95, 10, 10)]
    [InlineData(10, 95, 10)]
    [InlineData(10, 10, 90)]
    public void Critical_at_critical_thresholds(double cpu, double memory, double disk)
    {
        var result = ServerStatusEvaluator.Evaluate(Snapshot(cpu, memory, disk), Options);

        Assert.Equal(ServerStatus.Critical, result.Status);
    }

    [Fact]
    public void Critical_wins_over_warning_and_lists_all_reasons()
    {
        var result = ServerStatusEvaluator.Evaluate(Snapshot(85, 10, 20, 91), Options);

        Assert.Equal(ServerStatus.Critical, result.Status);
        Assert.Equal(["Disk /disk1 %91", "CPU %85"], result.Reasons);
    }

    [Fact]
    public void Max_disk_usage_uses_fullest_mount()
    {
        Assert.Equal(91, ServerStatusEvaluator.MaxDiskUsagePercent(Snapshot(0, 0, 20, 91, 50)));
        Assert.Equal(0, ServerStatusEvaluator.MaxDiskUsagePercent(Snapshot(0, 0)));
    }
}

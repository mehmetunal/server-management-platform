using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.ResourceUsage;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Tests.ResourceUsage;

public class ResourceHeuristicsTests
{
    private static readonly MemoryInfo HealthyMemory = new(8_000_000, 6_000_000, 4_000_000, 100_000, 1_000_000, 0, 2_000_000, 2_000_000);
    private static readonly CpuBreakdown IdleCpu = new(5, 2, 92, 1, 0, "vmstat");

    private static ResourceSnapshot Snapshot(
        double load = 0.5, MemoryInfo? memory = null, CpuBreakdown? cpu = null, IReadOnlyList<string>? oom = null) =>
        new()
        {
            CpuCores = 4,
            Load1 = load,
            Memory = memory ?? HealthyMemory,
            Cpu = cpu ?? IdleCpu,
            OomEvents = oom ?? [],
            OomSource = "journal"
        };

    private static StorageSnapshot Storage(int usePercent, int? inodePercent = null) =>
        new([new FileSystemEntry("/dev/sda1", "ext4", 100_000_000, 1, 5_000_000, usePercent, "/", inodePercent)], false, []);

    private static ProcessList Processes(params (string Command, double Cpu)[] processes) =>
        new(processes.Select((p, i) => new ProcessEntry(100 + i, 1, "root", p.Cpu, 1, 1000, 60, p.Command)).ToList(), true, processes.Length);

    [Fact]
    public void Healthy_server_gets_a_single_ok_finding()
    {
        var findings = ResourceHeuristics.Evaluate(Snapshot(), Processes(("nginx", 3)), Storage(40), []);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Ok, finding.Severity);
    }

    [Theory]
    [InlineData(4.5, FindingSeverity.Warning)]
    [InlineData(9.0, FindingSeverity.Critical)]
    public void Load_above_core_count_is_reported(double load, FindingSeverity severity)
    {
        var finding = Assert.Single(ResourceHeuristics.Evaluate(Snapshot(load), null, null, null));

        Assert.Equal(severity, finding.Severity);
        Assert.Equal(FindingAction.Cpu, finding.Action);
        Assert.Contains("çekirdek sayısı 4", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_equal_to_cores_is_not_reported() =>
        Assert.Equal(FindingSeverity.Ok, Assert.Single(ResourceHeuristics.Evaluate(Snapshot(4.0), null, null, null)).Severity);

    [Fact]
    public void High_iowait_is_critical_and_links_to_io_section()
    {
        var findings = ResourceHeuristics.Evaluate(Snapshot(cpu: new CpuBreakdown(10, 5, 50, 35, 0, "vmstat")), null, null, null);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Equal(FindingAction.Io, finding.Action);
    }

    [Fact]
    public void Low_memory_and_high_swap_are_reported()
    {
        var memory = new MemoryInfo(8_000_000, 300_000, 50_000, 10_000, 200_000, 0, 2_000_000, 200_000);

        var findings = ResourceHeuristics.Evaluate(Snapshot(memory: memory), null, null, null);

        Assert.Contains(findings, f => f.Title == "Kullanılabilir bellek çok az" && f.Severity == FindingSeverity.Critical);
        Assert.Contains(findings, f => f.Title == "Swap kullanımı yüksek" && f.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public void Small_swap_usage_is_ignored()
    {
        var memory = HealthyMemory with { SwapTotalKilobytes = 40_000, SwapFreeKilobytes = 0 };

        Assert.Equal(FindingSeverity.Ok, Assert.Single(ResourceHeuristics.Evaluate(Snapshot(memory: memory), null, null, null)).Severity);
    }

    [Theory]
    [InlineData(89, null)]
    [InlineData(90, FindingSeverity.Warning)]
    [InlineData(97, FindingSeverity.Critical)]
    public void Disk_usage_over_ninety_percent_links_to_cleanup(int percent, FindingSeverity? expected)
    {
        var findings = ResourceHeuristics.Evaluate(Snapshot(), null, Storage(percent), null);
        var disk = findings.SingleOrDefault(f => f.Action == FindingAction.Cleanup);

        Assert.Equal(expected, disk?.Severity);
    }

    [Fact]
    public void Inode_exhaustion_is_reported()
    {
        var findings = ResourceHeuristics.Evaluate(Snapshot(), null, Storage(20, 96), null);

        var finding = Assert.Single(findings);
        Assert.Contains("inode", finding.Title, StringComparison.Ordinal);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
    }

    [Fact]
    public void Single_process_over_eighty_percent_cpu_is_reported()
    {
        var findings = ResourceHeuristics.Evaluate(Snapshot(), Processes(("/usr/bin/node server.js", 185), ("nginx", 2)), null, null);

        var finding = Assert.Single(findings);
        Assert.Equal("node CPU'yu yoğun kullanıyor", finding.Title);
        Assert.Equal(FindingAction.Processes, finding.Action);
    }

    [Fact]
    public void Oom_kills_are_critical()
    {
        var findings = ResourceHeuristics.Evaluate(Snapshot(oom: ["Out of memory: Killed process 42 (java)"]), null, null, null);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Contains("son 24 saatte", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Steal_time_is_reported()
    {
        var finding = Assert.Single(ResourceHeuristics.Evaluate(Snapshot(cpu: new CpuBreakdown(20, 5, 60, 0, 15, "vmstat")), null, null, null));

        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Contains("steal", finding.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Busy_container_and_memory_limit_are_reported()
    {
        var containers = new[]
        {
            new ContainerUsage(new DockerContainerStatsDto { Name = "api", CpuPercent = 150 }, null, null),
            new ContainerUsage(new DockerContainerStatsDto { Name = "db", MemoryPercent = 95, MemoryUsageBytes = 950, MemoryLimitBytes = 1000 }, null, null)
        };

        var findings = ResourceHeuristics.Evaluate(Snapshot(), null, null, containers);

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(FindingAction.Containers, f.Action));
    }

    [Fact]
    public void Findings_are_ordered_by_severity()
    {
        var findings = ResourceHeuristics.Evaluate(
            Snapshot(load: 5, oom: ["Out of memory"]), null, Storage(91), null);

        Assert.Equal(FindingSeverity.Critical, findings[0].Severity);
        Assert.True(findings.Select(f => (int)f.Severity).SequenceEqual(findings.Select(f => (int)f.Severity).Order()));
    }
}

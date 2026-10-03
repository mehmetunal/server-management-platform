using ServerManager.Application.Tests.TestData;
using ServerManager.Infrastructure.Monitoring;

namespace ServerManager.Application.Tests.Monitoring;

public class LinuxMetricsParserTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parses_cpu_from_two_proc_stat_samples()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.Full, Now);

        Assert.Equal(30, snapshot.CpuUsagePercent);
        Assert.Equal(2, snapshot.CpuCores);
        Assert.Equal(4, snapshot.CpuThreads);
        Assert.Equal(0.52, snapshot.LoadAverage1);
        Assert.Equal(0.31, snapshot.LoadAverage15);
        Assert.Equal(48.5, snapshot.CpuTemperatureCelsius);
    }

    [Fact]
    public void Parses_memory_using_mem_available()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.Full, Now);

        Assert.Equal(8_000_000L * 1024, snapshot.MemoryTotalBytes);
        Assert.Equal(2_000_000L * 1024, snapshot.MemoryUsedBytes);
        Assert.Equal(25, snapshot.MemoryUsagePercent);
        Assert.Equal(2_200_000L * 1024, snapshot.MemoryCachedBytes);
        Assert.Equal(500_000L * 1024, snapshot.SwapUsedBytes);
    }

    [Fact]
    public void Filters_pseudo_and_bind_mounted_file_systems()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.Full, Now);

        Assert.Equal(["/", "/data"], snapshot.Disks.Select(d => d.MountPoint));
        var data = snapshot.Disks.Single(d => d.MountPoint == "/data");
        Assert.Equal(92, data.UsagePercent);
        Assert.Equal(80, data.InodeUsagePercent);
        Assert.Equal(50_000_000L * 1024, data.TotalBytes);
    }

    [Fact]
    public void Calculates_network_rates_and_marks_virtual_interfaces()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.Full, Now);

        var eth0 = snapshot.NetworkInterfaces.Single(i => i.Name == "eth0");
        Assert.False(eth0.IsVirtual);
        Assert.Equal(102_400, eth0.RxBytesPerSecond);
        Assert.Equal(51_200, eth0.TxBytesPerSecond);
        Assert.Equal(["10.0.0.5/24", "fe80::1/64"], eth0.Addresses);
        Assert.True(snapshot.NetworkInterfaces.Single(i => i.Name == "lo").IsVirtual);
        Assert.True(snapshot.NetworkInterfaces.Single(i => i.Name == "veth12ab").IsVirtual);
        Assert.Equal("eth0", snapshot.NetworkInterfaces[0].Name);
        Assert.DoesNotContain(snapshot.NetworkInterfaces, i => i.Name is "gre0" or "sit0");
    }

    [Fact]
    public void Parses_processes_and_skips_collector_commands()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.Full, Now);

        Assert.Equal(["nginx", "systemd", "mysqld"], snapshot.TopProcesses.Select(p => p.Name));
        var nginx = snapshot.TopProcesses[0];
        Assert.Equal(1234, nginx.Pid);
        Assert.Equal("www-data", nginx.User);
        Assert.Equal(12.5, nginx.CpuPercent);
    }

    [Fact]
    public void Parses_system_information()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.Full, Now);

        Assert.Equal("web-01", snapshot.System.Hostname);
        Assert.Equal("Ubuntu 24.04.1 LTS", snapshot.System.OperatingSystem);
        Assert.Equal("6.8.0-45-generic", snapshot.System.Kernel);
        Assert.Equal("x86_64", snapshot.System.Architecture);
        Assert.Equal("Europe/Istanbul", snapshot.System.Timezone);
        Assert.Equal(1001, snapshot.System.UptimeSeconds);
        Assert.Equal(Now.AddSeconds(-1001), snapshot.System.BootTimeUtc);
    }

    [Fact]
    public void Handles_busybox_output_with_missing_optional_sections()
    {
        var snapshot = LinuxMetricsParser.Parse(LinuxMetricsSamples.BusyBoxMinimal, Now);

        Assert.Equal(10, snapshot.CpuUsagePercent);
        Assert.Equal(2, snapshot.CpuCores);
        Assert.Equal(40, snapshot.MemoryUsagePercent);
        Assert.Equal(0, snapshot.SwapTotalBytes);
        Assert.Equal(2000, snapshot.NetworkInterfaces.Single().RxBytesPerSecond);
        Assert.Single(snapshot.Disks);
        Assert.Null(snapshot.Disks[0].InodeUsagePercent);
        Assert.Empty(snapshot.TopProcesses);
        Assert.Null(snapshot.CpuTemperatureCelsius);
        Assert.Equal("Alpine Linux", snapshot.System.OperatingSystem);
        Assert.Equal("UTC", snapshot.System.Timezone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bash: sh: command not found")]
    [InlineData("@@T1\n1.0 1.0\n@@STAT1\ncpu 1 1 1 1\n")]
    public void Throws_format_exception_for_incomplete_output(string output)
    {
        Assert.Throws<FormatException>(() => LinuxMetricsParser.Parse(output, Now));
    }

    [Fact]
    public void Cpu_usage_is_zero_when_counters_do_not_advance()
    {
        Assert.Equal(0, LinuxMetricsParser.CalculateCpuUsage("cpu 10 0 10 80 0 0 0 0", "cpu 10 0 10 80 0 0 0 0"));
    }

    [Fact]
    public void Script_has_no_single_quotes_inside_sh_wrapper()
    {
        var command = LinuxMetricsScript.Command;
        var inner = command["sh -c '".Length..^1];

        Assert.StartsWith("sh -c '", command);
        Assert.EndsWith("'", command);
        Assert.DoesNotContain('\'', inner);
        Assert.Contains(LinuxMetricsScript.EndMarker, inner);
    }
}

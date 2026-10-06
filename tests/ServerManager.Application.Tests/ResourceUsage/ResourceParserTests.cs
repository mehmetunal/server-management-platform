using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.ResourceUsage;
using ServerManager.Infrastructure.Docker;
using ServerManager.Infrastructure.ServerSystem;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Application.Tests.ResourceUsage;

public class ResourceParserTests
{
    [Fact]
    public void ParseSnapshot_reads_load_memory_vmstat_swap_oom_and_pidstat()
    {
        const string output = """
            @@ss:nproc
            4
            @@ss:loadavg
            5.10 3.20 1.05 3/412 12345
            @@ss:uptime
            86400.55 300000.10
            @@ss:meminfo
            MemTotal:        8000000 kB
            MemFree:          200000 kB
            MemAvailable:     600000 kB
            Buffers:          100000 kB
            Cached:           300000 kB
            SReclaimable:      50000 kB
            Shmem:             20000 kB
            SwapTotal:       2000000 kB
            SwapFree:         500000 kB
            @@ss:cpu
            tool=vmstat
            procs -----------memory---------- ---swap-- -----io---- -system-- -------cpu-------
             r  b   swpd   free   buff  cache   si   so    bi    bo   in   cs us sy id wa st gu
             1  0      0 123456  12345 234567    0    0     1     2   10   20  1  1 98  0  0  0
             3  2 1500000 200000 100000 300000   50   80  9000  4000 2000 3000 30 10 25 33  2  0
            @@ss:swap
            /proc/101/status:Name:	java
            /proc/101/status:VmSwap:	  900000 kB
            /proc/202/status:Name:	Web Content
            /proc/202/status:VmSwap:	   12000 kB
            /proc/303/status:Name:	sshd
            /proc/303/status:VmSwap:	       0 kB
            @@ss:oom
            source=journal
            2026-10-05T03:12:01+0000 host kernel: Out of memory: Killed process 4242 (java) total-vm:9000000kB
            @@ss:io
            tool=pidstat
            Linux 6.8.0 (host)  10/05/26  _x86_64_  (4 CPU)

            12:00:01 AM   UID       PID   kB_rd/s   kB_wr/s kB_ccwr/s iodelay  Command
            12:00:02 AM     0       123      0.00   2048.00      0.00       5  jbd2/sda1-8

            Average:      UID       PID   kB_rd/s   kB_wr/s kB_ccwr/s iodelay  Command
            Average:        0       123      0.00   2048.00      0.00       -  jbd2/sda1-8
            Average:      999      4242   5120.00      8.00      0.00       -  postgres: checkpointer
            Average:        0       555      0.00      0.00      0.00       -  idle
            @@ss:end
            """;

        var snapshot = ResourceParser.ParseSnapshot(output);

        Assert.Equal(4, snapshot.CpuCores);
        Assert.Equal(5.10, snapshot.Load1);
        Assert.Equal(1.05, snapshot.Load15);
        Assert.Equal(86400, snapshot.UptimeSeconds);

        var memory = snapshot.Memory!;
        Assert.Equal(8000000, memory.TotalKilobytes);
        Assert.Equal(7400000, memory.UsedKilobytes);
        Assert.Equal(450000, memory.CacheKilobytes);
        Assert.Equal(1500000, memory.SwapUsedKilobytes);
        Assert.Equal(75, memory.SwapUsedPercent);

        var cpu = snapshot.Cpu!;
        Assert.Equal("vmstat", cpu.Source);
        Assert.Equal(30, cpu.User);
        Assert.Equal(33, cpu.IoWait);
        Assert.Equal(2, cpu.Steal);
        Assert.Equal(25, cpu.Idle);

        Assert.Equal([101, 202], snapshot.SwapProcesses.Select(p => p.Pid));
        Assert.Equal("Web Content", snapshot.SwapProcesses[1].Name);

        Assert.Equal("journal", snapshot.OomSource);
        Assert.Single(snapshot.OomEvents);

        Assert.True(snapshot.IoToolAvailable);
        Assert.Equal([4242, 123], snapshot.IoProcesses.Select(p => p.Pid));
        Assert.Equal("postgres: checkpointer", snapshot.IoProcesses[0].Command);
        Assert.Equal("999", snapshot.IoProcesses[0].User);
    }

    [Fact]
    public void ParseCpu_falls_back_to_proc_stat_delta()
    {
        var cpu = ResourceParser.ParseCpu(
        [
            "tool=proc",
            "cpu  1000 0 500 8000 100 0 0 0 0 0",
            "cpu  1060 0 520 8100 120 0 0 0 0 0"
        ]);

        Assert.NotNull(cpu);
        Assert.Equal("/proc/stat", cpu.Source);
        Assert.Equal(30, cpu.User);
        Assert.Equal(10, cpu.System);
        Assert.Equal(50, cpu.Idle);
        Assert.Equal(10, cpu.IoWait);
    }

    [Fact]
    public void ParseMemInfo_estimates_available_on_old_kernels()
    {
        var memory = ResourceParser.ParseMemInfo(["MemTotal: 1000 kB", "MemFree: 100 kB", "Buffers: 50 kB", "Cached: 150 kB"]);

        Assert.Equal(300, memory!.AvailableKilobytes);
        Assert.Null(memory.SwapUsedPercent);
    }

    [Fact]
    public void ParsePidstat_handles_versions_without_uid_and_iodelay()
    {
        var processes = ResourceParser.ParsePidstat(
        [
            "Average:       PID   kB_rd/s   kB_wr/s kB_ccwr/s  Command",
            "Average:       777      1.50      0.00      0.00  mysqld"
        ]);

        var process = Assert.Single(processes);
        Assert.Null(process.User);
        Assert.Equal("mysqld", process.Command);
        Assert.Equal(1.5, process.ReadKilobytesPerSecond);
    }

    [Fact]
    public void ParseDiskScan_reads_du_find_and_timeouts()
    {
        const string output = """
            @@ss:du
            8388608	/var
            6291456	/var/lib
            1048576	/var/log
            20480	/var/lib/apt

            @@ss:dustatus
            124
            @@ss:files
            2147483648|1727000000|root|/var/lib/docker/containers/abc/abc-json.log
            209715200|1727000000|postgres|/srv/backup dump|1.sql
            garbage line
            @@ss:filesstatus
            0
            @@ss:end
            """;

        var report = ResourceParser.ParseDiskScan("/var", output);

        Assert.Equal("/var", report.Path);
        Assert.Equal(["/var", "/var/lib", "/var/log", "/var/lib/apt"], report.Directories.Select(d => d.Path));
        Assert.Equal([0, 1, 1, 2], report.Directories.Select(d => d.Depth));
        Assert.True(report.DirectoriesTimedOut);
        Assert.False(report.FilesTimedOut);
        Assert.Equal(2, report.Files.Count);
        Assert.Equal("/srv/backup dump|1.sql", report.Files[1].Path);
        Assert.Equal("postgres", report.Files[1].Owner);
        Assert.Equal(2147483648, report.Files[0].SizeBytes);
    }

    [Theory]
    [InlineData("124", true)]
    [InlineData("143", true)]
    [InlineData("0", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void Timeout_exit_codes_of_gnu_and_busybox_are_recognised(string? status, bool expected) =>
        Assert.Equal(expected, ResourceParser.IsTimeout(status));

    [Fact]
    public void DiskScan_command_quotes_path_and_limits_runtime()
    {
        var command = ResourceCommands.DiskScan("/srv/it's here");

        Assert.StartsWith("sh -c '", command, StringComparison.Ordinal);
        var inner = "p=" + ShellQuote.Quote("/srv/it's here");
        Assert.Contains(inner.Replace("'", "'\"'\"'", StringComparison.Ordinal), command, StringComparison.Ordinal);
        Assert.Contains("timeout 60", command, StringComparison.Ordinal);
        Assert.Contains("nice -n 19", command, StringComparison.Ordinal);
        Assert.Contains("ionice -c3", command, StringComparison.Ordinal);
        Assert.Contains("du -x -k -d 2", command, StringComparison.Ordinal);
        Assert.Contains("--threshold=10M", command, StringComparison.Ordinal);
        Assert.Contains("-xdev -type f -size +102400k", command, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinStats_maps_labels_by_name_then_id()
    {
        const string ps = """
            {"ID":"aaa111","Names":"sm-shop","Labels":"sm.project=shop"}
            {"ID":"bbb222","Names":"sm-svc-db","Labels":"sm.service=db,sm.managed=true"}
            """;
        var labels = DockerHousekeepingParser.ParseContainerLabels(ps);

        var joined = DockerHousekeepingParser.JoinStats(
        [
            new DockerContainerStatsDto { Id = "aaa111", Name = "sm-shop" },
            new DockerContainerStatsDto { Id = "bbb222", Name = "renamed" },
            new DockerContainerStatsDto { Id = "ccc333", Name = "other" }
        ], labels);

        Assert.Equal("shop", joined[0].Labels["sm.project"]);
        Assert.Equal("db", joined[1].Labels["sm.service"]);
        Assert.Empty(joined[2].Labels);
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/var/lib/docker", true)]
    [InlineData("/srv/my data", true)]
    [InlineData("var", false)]
    [InlineData("/var/../etc", false)]
    [InlineData("/proc", false)]
    [InlineData("/sys/fs", false)]
    [InlineData("/dev", false)]
    [InlineData("/var/log\n", false)]
    [InlineData("", false)]
    public void Scan_path_validation(string path, bool valid) =>
        Assert.Equal(valid, ResourceRules.IsValidScanPath(path));

    [Fact]
    public void NormalizePath_collapses_slashes_and_defaults_to_root()
    {
        Assert.Equal("/var/log", ResourceRules.NormalizePath("//var//log/"));
        Assert.Equal("/", ResourceRules.NormalizePath(null));
        Assert.Equal("/", ResourceRules.NormalizePath("/"));
    }
}

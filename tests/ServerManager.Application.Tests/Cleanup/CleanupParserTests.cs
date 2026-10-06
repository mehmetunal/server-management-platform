using ServerManager.Infrastructure.Docker;
using ServerManager.Infrastructure.ServerSystem;

namespace ServerManager.Application.Tests.Cleanup;

public class CleanupParserTests
{
    [Fact]
    public void ParseDiskUsage_reads_docker_system_df_verbose_json()
    {
        // docker 28 "docker system df -v --format '{{json .}}'" çıktısından kısaltılmış örnek.
        const string output = """
            {"Images":[{"Containers":"1","CreatedAt":"2026-10-03 22:07:43 +0000 UTC","ID":"sha256:594c","Repository":"sm-demo","SharedSize":"8.8MB","Size":"8.82MB","Tag":"84df9f00ef45","UniqueSize":"0B"},
            {"Containers":"0","CreatedAt":"2026-09-17 21:33:33 +0000 UTC","ID":"sha256:ef73","Repository":"postgres","Size":"288MB","Tag":"16-alpine"},
            {"Containers":"N/A","ID":"sha256:9999","Repository":"<none>","Size":"1GB","Tag":"<none>"}],
            "Containers":[{"ID":"b94b","Image":"alpine:3","Labels":"","Names":"failed-job","Size":"89B","State":"exited","Status":"Exited (3) 2 days ago"},
            {"ID":"3a06","Image":"sm-sudo-demo:84df","Labels":"sm.project=sudo-demo,com.docker.compose.project=sm-sudo-demo","Names":"sm-sudo-demo","Size":"1.2kB (virtual 9MB)","State":"running","Status":"Up 58 minutes"}],
            "Volumes":[{"Driver":"local","Labels":"","Links":"0","Name":"orphan-vol","Size":"1.393kB"},{"Labels":"sm.managed=true","Links":"1","Name":"sm-svc-db-data","Size":"N/A"}],
            "BuildCache":[{"ID":"x4i4","InUse":"false","Shared":"false","Size":"13MB"},{"ID":"u7d0","InUse":"true","Shared":"false","Size":"100MB"},{"ID":"bk1z","InUse":"false","Shared":"true","Size":"50MB"}]}
            """;

        var usage = DockerHousekeepingParser.ParseDiskUsage(output);

        Assert.Equal(3, usage.Images.Count);
        Assert.Equal(1, usage.Images[0].Containers);
        Assert.Equal(-1, usage.Images[2].Containers);
        Assert.True(usage.Images[2].IsDangling);
        Assert.Equal(288_000_000, usage.Images[1].SizeBytes);
        Assert.Equal("postgres:16-alpine", usage.Images[1].Reference);

        var running = usage.Containers.Single(c => c.Id == "3a06");
        Assert.Equal("sudo-demo", running.Labels["sm.project"]);
        Assert.Equal("sm-sudo-demo", running.ComposeProject);
        Assert.Equal(1200, running.SizeBytes);
        Assert.Equal("exited", usage.Containers.Single(c => c.Id == "b94b").State);

        Assert.Equal(0, usage.Volumes[0].Links);
        Assert.Equal(1393, usage.Volumes[0].SizeBytes);
        Assert.Null(usage.Volumes[1].SizeBytes);
        Assert.Equal("true", usage.Volumes[1].Labels["sm.managed"]);

        // Kullanımdaki ve paylaşılan cache boyuta eklenmez; paylaşılan kayıt yine de sayılır.
        Assert.Equal(13_000_000, usage.BuildCacheReclaimableBytes);
        Assert.Equal(2, usage.BuildCacheEntries);
    }

    [Fact]
    public void ParseDiskUsage_returns_empty_for_invalid_output()
    {
        var usage = DockerHousekeepingParser.ParseDiskUsage("Cannot connect to the Docker daemon");

        Assert.Empty(usage.Images);
        Assert.Empty(usage.Volumes);
        Assert.Equal(0, usage.BuildCacheReclaimableBytes);
    }

    [Fact]
    public void ParseNetworks_reads_unused_network_list_with_labels()
    {
        const string output = """
            {"CreatedAt":"2026-10-03 18:31:49 +0000 UTC","Driver":"bridge","ID":"ea60976ce88c","Labels":"","Name":"appnet","Scope":"local"}
            {"Driver":"bridge","ID":"c268e07189e1","Labels":"com.docker.compose.network=default,com.docker.compose.project=sm-demo","Name":"sm-demo_default","Scope":"local"}
            """;

        var networks = DockerHousekeepingParser.ParseNetworks(output);

        Assert.Equal(["appnet", "sm-demo_default"], networks.Select(n => n.Name));
        Assert.Equal("sm-demo", networks[1].Labels["com.docker.compose.project"]);
    }

    [Theory]
    [InlineData("Archived and active journals take up 1.2G in the file system.", 1288490189L)]
    [InlineData("Journals take up 48.0M on disk.", 50331648L)]
    [InlineData("Archived and active journals take up 512.0K in the file system.", 524288L)]
    [InlineData("No journal files were found.", 0L)]
    public void ParseJournalUsage_reads_journalctl_disk_usage(string output, long expected) =>
        Assert.Equal(expected, CleanupParser.ParseJournalUsage(output));

    [Fact]
    public void ParseJournalUsage_returns_null_for_permission_error() =>
        Assert.Null(CleanupParser.ParseJournalUsage("Failed to open journal: Permission denied"));

    [Theory]
    [InlineData("apt 204800", "apt", 209715200L)]
    [InlineData("dnf ", "dnf", null)]
    [InlineData("pacman 10", null, null)]
    public void ParsePackageManager_reads_manager_and_cache_size(string line, string? manager, long? bytes)
    {
        var (parsedManager, parsedBytes) = CleanupParser.ParsePackageManager(line);

        Assert.Equal(manager, parsedManager);
        Assert.Equal(bytes, parsedBytes);
    }

    [Fact]
    public void ParseSystem_reads_file_sets_snaps_and_kernels()
    {
        const string output = """
            @@ss:pm
            apt 1024
            @@ss:journal
            available
            Archived and active journals take up 2.0G in the file system.
            @@ss:logs
            5242880|1727000000|/var/log/syslog.2.gz
            1024|1727000000|/var/log/app|with|pipes.1
            @total|2|5243904
            @@ss:tmp
            @total|0|0
            @@ss:snap
            available
            Name    Version   Rev    Tracking       Publisher   Notes
            core20  20240111  2182   latest/stable  canonical✓  base,disabled
            core20  20240227  2264   latest/stable  canonical✓  base
            lxd     5.0.3     27037  5.0/stable     canonical✓  disabled
            @@ss:snapfiles
            67108864|/var/lib/snapd/snaps/core20_2182.snap
            @@ss:kernel
            6.8.0-45-generic
            @@ss:kernels
            install ok installed|linux-image-6.8.0-40-generic|12345
            install ok installed|linux-image-6.8.0-45-generic|12400
            deinstall ok config-files|linux-image-6.5.0-10-generic|12000
            @@ss:end
            """;

        var facts = CleanupParser.ParseSystem(output);

        Assert.Equal("apt", facts.PackageManager);
        Assert.Equal(1024 * 1024, facts.PackageCacheBytes);
        Assert.Equal(2L * 1024 * 1024 * 1024, facts.JournalBytes);
        Assert.Equal(2, facts.RotatedLogs.Count);
        Assert.Equal(5243904, facts.RotatedLogs.TotalBytes);
        Assert.Equal("/var/log/app|with|pipes.1", facts.RotatedLogs.Largest[1].Path);
        Assert.Equal(0, facts.TempFiles.Count);
        Assert.Equal(["core20", "lxd"], facts.DisabledSnaps.Select(s => s.Name));
        Assert.Equal(67108864, facts.DisabledSnaps[0].SizeBytes);
        Assert.Null(facts.DisabledSnaps[1].SizeBytes);
        Assert.Equal("6.8.0-45-generic", facts.RunningKernel);
        Assert.Equal(["6.8.0-40-generic", "6.8.0-45-generic"], facts.Kernels.Select(k => k.Version));
        Assert.Equal(12345L * 1024, facts.Kernels[0].SizeBytes);
    }

    [Fact]
    public void ParseKernels_reads_rpm_and_deduplicates_versions()
    {
        var kernels = CleanupParser.ParseKernels(
        [
            "kernel|5.14.0-427.el9.x86_64|0",
            "kernel-core|5.14.0-427.el9.x86_64|98765432",
            "kernel-core|5.14.0-503.el9.x86_64|99999999",
            "package kernel-core is not installed"
        ]);

        Assert.Equal(2, kernels.Count);
        Assert.Equal(98765432, kernels[0].SizeBytes);
    }

    [Fact]
    public void TotalAvailableBytes_counts_each_device_once()
    {
        const string output = """
            @@ss:df
            Filesystem     Type  1024-blocks    Used Available Capacity Mounted on
            /dev/sda1      ext4     41152736 30000000  9000000      77% /
            /dev/sda1      ext4     41152736 30000000  9000000      77% /var/lib/docker/overlay2
            /dev/sdb1      xfs      10000000  1000000  1000      10% /data
            @@ss:end
            """;

        Assert.Equal((9000000L + 1000) * 1024, CleanupParser.TotalAvailableBytes(output));
    }
}

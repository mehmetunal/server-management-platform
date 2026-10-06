using ServerManager.Application.Cleanup;
using ServerManager.Infrastructure.Docker;
using ServerManager.Infrastructure.ServerSystem;

namespace ServerManager.Application.Tests.Cleanup;

public class CleanupCommandsTests
{
    private static CleanupItem Item(CleanupCategory category, string target, string key = "k") =>
        new(key, category, target, target, null, 0, CleanupSafety.Safe, null, true);

    [Fact]
    public void Scan_script_is_single_quoted_sh_with_sections_and_day_thresholds()
    {
        var script = CleanupCommands.Scan(new CleanupOptions { LogDays = 21, TempDays = 3, JournalMaxMegabytes = 100 });

        Assert.StartsWith("sh -c '", script, StringComparison.Ordinal);
        Assert.EndsWith("'", script, StringComparison.Ordinal);
        foreach (var section in new[] { "pm", "journal", "logs", "tmp", "snap", "snapfiles", "kernel", "kernels", "end" })
            Assert.Contains("echo @@ss:" + section, script, StringComparison.Ordinal);
        Assert.Contains("-mtime +21", script, StringComparison.Ordinal);
        Assert.Contains("-mtime +3 -atime +3", script, StringComparison.Ordinal);
        Assert.Contains("journalctl --disk-usage", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Rotated_log_finder_skips_journal_and_active_logs()
    {
        var find = CleanupCommands.RotatedLogsFind(14);

        Assert.StartsWith("find /var/log -xdev -type f ", find, StringComparison.Ordinal);
        Assert.Contains("! -path '/var/log/journal/*'", find, StringComparison.Ordinal);
        Assert.Contains("-name '*.gz'", find, StringComparison.Ordinal);
        Assert.Contains("-name '*.[0-9]'", find, StringComparison.Ordinal);
        Assert.Contains("-name '*.old'", find, StringComparison.Ordinal);
        Assert.DoesNotContain("'*.log'", find, StringComparison.Ordinal);
        Assert.Contains("-mtime +14", find, StringComparison.Ordinal);
    }

    [Fact]
    public void Temp_finder_only_matches_regular_files_and_excludes_private_dirs()
    {
        var find = CleanupCommands.TempFilesFind(7);

        Assert.Contains("-type f", find, StringComparison.Ordinal);
        Assert.Contains("-mindepth 1", find, StringComparison.Ordinal);
        Assert.Contains("! -path '/tmp/systemd-private-*'", find, StringComparison.Ordinal);
        Assert.Contains("! -path '/tmp/.X11-unix/*'", find, StringComparison.Ordinal);
        Assert.Contains("-mtime +7 -atime +7", find, StringComparison.Ordinal);
    }

    [Fact]
    public void Day_values_are_clamped_before_entering_the_command()
    {
        Assert.Contains("-mtime +1 ", CleanupCommands.TempFilesFind(-5), StringComparison.Ordinal);
        Assert.Contains("-mtime +3650", CleanupCommands.RotatedLogsFind(int.MaxValue), StringComparison.Ordinal);
        Assert.Equal("journalctl --vacuum-size=16M 2>&1", CleanupCommands.VacuumJournal(1));
    }

    [Fact]
    public void Delete_uses_the_same_find_expression_as_preview()
    {
        var find = CleanupCommands.TempFilesFind(10);

        var preview = CleanupCommands.PreviewFiles(find);
        var delete = CleanupCommands.DeleteFiles(find);

        Assert.DoesNotContain("-delete", preview, StringComparison.Ordinal);
        Assert.Contains("-delete -print", delete, StringComparison.Ordinal);
        Assert.Contains(find.Replace("'", "'\"'\"'", StringComparison.Ordinal), delete, StringComparison.Ordinal);
        Assert.Contains(find.Replace("'", "'\"'\"'", StringComparison.Ordinal), preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Snap_values_are_shell_quoted()
    {
        Assert.Equal("snap remove 'core20' --revision='2182' 2>&1", CleanupCommands.RemoveSnapRevision("core20", "2182"));
        Assert.Equal("snap remove 'a'\"'\"'b' --revision='1' 2>&1", CleanupCommands.RemoveSnapRevision("a'b", "1"));
    }

    [Fact]
    public void Package_clean_supports_apt_simulation_only()
    {
        Assert.Equal("apt-get -s clean 2>&1", CleanupCommands.CleanPackages("apt", dryRun: true));
        Assert.Equal("apt-get clean 2>&1", CleanupCommands.CleanPackages("apt", dryRun: false));
        Assert.Equal("dnf clean all 2>&1", CleanupCommands.CleanPackages("dnf", dryRun: false));
        Assert.Null(CleanupCommands.CleanPackages("dnf", dryRun: true));
        Assert.Null(CleanupCommands.CleanPackages("pacman", dryRun: false));
    }

    [Fact]
    public void Docker_steps_use_docker_module_builders_with_quoting()
    {
        var options = new CleanupOptions();

        Assert.Equal("docker rm 'c1'", SshServerCleanupInspector.BuildStep(Item(CleanupCategory.Containers, "c1"), options, false)!.Command);
        Assert.Equal("docker image rm 'sm-shop:abc'", SshServerCleanupInspector.BuildStep(Item(CleanupCategory.Images, "sm-shop:abc"), options, false)!.Command);
        Assert.Equal("docker volume rm 'x'\"'\"'y'", SshServerCleanupInspector.BuildStep(Item(CleanupCategory.Volumes, "x'y"), options, false)!.Command);
        Assert.Equal("docker network rm 'n1'", SshServerCleanupInspector.BuildStep(Item(CleanupCategory.Networks, "n1"), options, false)!.Command);
        Assert.Equal(DockerCommands.PruneBuildCache, SshServerCleanupInspector.BuildStep(Item(CleanupCategory.BuildCache, "buildcache"), options, false)!.Command);
        Assert.Equal("docker builder prune -f", DockerCommands.PruneBuildCache);
        Assert.Contains("--filter dangling=true", DockerCommands.ListUnusedNetworks, StringComparison.Ordinal);
    }

    [Fact]
    public void Dry_run_never_builds_destructive_commands()
    {
        var options = new CleanupOptions();
        var items = new[]
        {
            Item(CleanupCategory.Containers, "c1"),
            Item(CleanupCategory.Images, "sha256:abc"),
            Item(CleanupCategory.Volumes, "v1"),
            Item(CleanupCategory.Networks, "n1"),
            Item(CleanupCategory.BuildCache, "buildcache"),
            Item(CleanupCategory.PackageCache, "apt"),
            Item(CleanupCategory.PackageCache, "dnf"),
            Item(CleanupCategory.Journal, "journal"),
            Item(CleanupCategory.RotatedLogs, "logs"),
            Item(CleanupCategory.TempFiles, "tmp"),
            Item(CleanupCategory.Snaps, "core20|2182")
        };

        foreach (var item in items)
        {
            var step = SshServerCleanupInspector.BuildStep(item, options, dryRun: true);
            Assert.NotNull(step);
            if (step.Command is null)
                continue;

            Assert.DoesNotContain("-delete", step.Command, StringComparison.Ordinal);
            Assert.DoesNotContain(" rm ", step.Command, StringComparison.Ordinal);
            Assert.DoesNotContain("prune", step.Command, StringComparison.Ordinal);
            Assert.DoesNotContain("vacuum", step.Command, StringComparison.Ordinal);
            Assert.DoesNotContain("snap remove", step.Command, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Kernels_have_no_cleanup_step()
    {
        Assert.Null(SshServerCleanupInspector.BuildStep(Item(CleanupCategory.Kernels, "linux-image-6.8.0-40-generic"), new CleanupOptions(), false));
    }
}

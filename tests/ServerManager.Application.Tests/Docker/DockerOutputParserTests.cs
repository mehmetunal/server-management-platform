using ServerManager.Application.Tests.TestData;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Application.Tests.Docker;

public class DockerOutputParserTests
{
    [Fact]
    public void Parses_overview_and_skips_warning_lines()
    {
        var overview = DockerOutputParser.ParseOverview(DockerSamples.Info, DockerSamples.Ps, "orphan-vol\nwebdata\n", "a\nb\nc\nd\n", DockerSamples.DiskUsage);

        Assert.Equal("28.5.2", overview.EngineVersion);
        Assert.Equal("overlay2", overview.StorageDriver);
        Assert.Equal(8, overview.CpuCount);
        Assert.Equal(8217473024, overview.MemoryTotalBytes);
        Assert.Equal(3, overview.ContainersTotal);
        Assert.Equal(1, overview.ContainersRunning);
        Assert.Equal(2, overview.ContainersStopped);
        Assert.Equal(1, overview.ContainersFailed);
        Assert.Equal(1, overview.ContainersUnhealthy);
        Assert.Equal(3, overview.Images);
        Assert.Equal(2, overview.Volumes);
        Assert.Equal(4, overview.Networks);
        Assert.Equal(4, overview.DiskUsage.Count);

        var images = overview.DiskUsage[0];
        Assert.Equal("Images", images.Type);
        Assert.Equal(181_000_000, images.SizeBytes);
        Assert.Equal(8_660_000, images.ReclaimableBytes);
        Assert.Equal(3, images.ActiveCount);
    }

    [Fact]
    public void Counts_containers_from_ps_when_info_is_missing()
    {
        var overview = DockerOutputParser.ParseOverview(string.Empty, DockerSamples.Ps, null, null, null);

        Assert.Null(overview.EngineVersion);
        Assert.Equal(3, overview.ContainersTotal);
        Assert.Equal(0, overview.Volumes);
        Assert.Empty(overview.DiskUsage);
    }

    [Fact]
    public void Parses_containers_ordered_by_state_then_name()
    {
        var containers = DockerOutputParser.ParseContainers(DockerSamples.Ps, null);

        Assert.Equal(["web", "cache", "failed-job"], containers.Select(c => c.Name));

        var web = containers[0];
        Assert.Equal(DockerSamples.WebId, web.Id);
        Assert.Equal("running", web.State);
        Assert.Equal("unhealthy", web.Health);
        Assert.Equal(["appnet", "bridge"], web.Networks);
        Assert.Contains("0.0.0.0:8080->80/tcp", web.Ports);
        Assert.Null(web.ExitCode);
        Assert.Equal(new DateTime(2026, 10, 3, 18, 31, 49, DateTimeKind.Utc), web.CreatedAt);

        Assert.Equal("shop", containers[1].ComposeProject);
        Assert.Equal(0, containers[1].ExitCode);
        Assert.Equal(3, containers[2].ExitCode);
    }

    [Fact]
    public void Container_details_from_inspect_override_ps_values()
    {
        var containers = DockerOutputParser.ParseContainers(DockerSamples.Ps, DockerSamples.WebInspect);
        var web = containers.Single(c => c.Name == "web");

        Assert.Equal("healthy", web.Health);
        Assert.Equal(2, web.RestartCount);
        Assert.Equal("shop", web.ComposeProject);
        Assert.Equal(new DateTime(2026, 10, 3, 18, 45, 13, 224, DateTimeKind.Utc).AddTicks(2480), web.StartedAt);
    }

    [Fact]
    public void Parses_container_details()
    {
        var details = DockerOutputParser.ParseContainerDetails(DockerSamples.WebInspect);

        Assert.NotNull(details);
        Assert.Equal("web", details.Container.Name);
        Assert.Equal("running", details.Container.State);
        Assert.Equal("nginx -g daemon off;", details.Command);
        Assert.Equal("/docker-entrypoint.sh", details.Entrypoint);
        Assert.Null(details.WorkingDir);
        Assert.Null(details.User);
        Assert.Equal("on-failure (en fazla 5)", details.RestartPolicy);
        Assert.Equal(["0.0.0.0:8080 → 80/tcp", ":::8080 → 80/tcp", "443/tcp"], details.PortBindings);

        var mount = Assert.Single(details.Mounts);
        Assert.Equal("webdata", mount.Name);
        Assert.Equal("/usr/share/nginx/html", mount.Destination);
        Assert.True(mount.ReadWrite);

        var appnet = details.NetworkDetails.Single(n => n.Name == "appnet");
        Assert.Equal("172.30.0.2", appnet.IpAddress);
        Assert.Equal(["web", "frontend"], appnet.Aliases);
        var bridge = details.NetworkDetails.Single(n => n.Name == "bridge");
        Assert.Empty(bridge.Aliases);
        Assert.Null(bridge.MacAddress);

        Assert.Equal("shop", details.Labels["com.docker.compose.project"]);
    }

    [Fact]
    public void Container_details_expose_only_environment_keys_and_mask_inspect_values()
    {
        var details = DockerOutputParser.ParseContainerDetails(DockerSamples.WebInspect);

        Assert.NotNull(details);
        Assert.Equal(["API_SECRET", "PATH", "EMPTY"], details.EnvironmentKeys);
        Assert.DoesNotContain("supersecret", details.InspectJson);
        Assert.DoesNotContain("/usr/local/sbin", details.InspectJson);
        Assert.Contains($"API_SECRET={DockerOutputParser.MaskedValue}", details.InspectJson);
        Assert.Contains("NGINX Docker Maintainers <docker-maint@nginx.com>", details.InspectJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("Error: No such object")]
    public void Container_details_return_null_for_empty_or_invalid_output(string output)
    {
        Assert.Null(DockerOutputParser.ParseContainerDetails(output));
    }

    [Fact]
    public void Parses_stats()
    {
        var stats = Assert.Single(DockerOutputParser.ParseStats(DockerSamples.Stats));

        Assert.Equal("web", stats.Name);
        Assert.Equal(0.12, stats.MemoryPercent);
        Assert.Equal((long)Math.Round(9.207 * 1024 * 1024), stats.MemoryUsageBytes);
        Assert.Equal((long)Math.Round(7.653 * 1024 * 1024 * 1024), stats.MemoryLimitBytes);
        Assert.Equal(3100, stats.NetworkRxBytes);
        Assert.Equal(252, stats.NetworkTxBytes);
        Assert.Equal(0, stats.BlockReadBytes);
        Assert.Equal(12300, stats.BlockWriteBytes);
        Assert.Equal(13, stats.Pids);
    }

    [Fact]
    public void Parses_images_with_dangling_last()
    {
        var images = DockerOutputParser.ParseImages(DockerSamples.Images);

        Assert.Equal(["alpine:3", "nginx:alpine"], images.Take(2).Select(i => i.Reference));

        var dangling = images[^1];
        Assert.True(dangling.IsDangling);
        Assert.Null(dangling.Digest);
        Assert.Null(dangling.ContainerCount);
        Assert.Equal(dangling.Id, dangling.Reference);

        Assert.Equal(62_400_000, images[1].SizeBytes);
        Assert.Equal(1, images[1].ContainerCount);
    }

    [Fact]
    public void Parses_volumes_with_usage_and_sizes()
    {
        var volumes = DockerOutputParser.ParseVolumes(DockerSamples.Volumes, DockerSamples.Ps, DockerSamples.DiskUsageVerbose);

        Assert.Equal(["webdata", "orphan-vol"], volumes.Select(v => v.Name));
        Assert.Equal(["web"], volumes[0].Containers);
        Assert.Equal(1393, volumes[0].SizeBytes);
        Assert.Equal("shop", volumes[0].ComposeProject);
        Assert.Empty(volumes[1].Containers);
        Assert.Equal(0, volumes[1].SizeBytes);
    }

    [Fact]
    public void Volume_size_is_null_without_disk_usage()
    {
        var volumes = DockerOutputParser.ParseVolumes(DockerSamples.Volumes, null, null);

        Assert.All(volumes, v => Assert.Null(v.SizeBytes));
    }

    [Fact]
    public void Parses_networks_with_system_networks_last()
    {
        var networks = DockerOutputParser.ParseNetworks(DockerSamples.Networks, DockerSamples.NetworkInspect);

        Assert.Equal(["appnet", "bridge", "host", "none"], networks.Select(n => n.Name));

        var appnet = networks[0];
        Assert.False(appnet.IsSystem);
        Assert.Equal(["172.30.0.0/16"], appnet.Subnets);
        Assert.Equal(["172.30.0.1"], appnet.Gateways);
        var member = Assert.Single(appnet.Containers);
        Assert.Equal("web", member.Name);
        Assert.Equal("172.30.0.2/16", member.IpAddress);

        Assert.All(networks.Skip(1), n => Assert.True(n.IsSystem));
        Assert.True(networks[^1].Internal);
        Assert.Empty(networks[1].Containers);
    }

    [Fact]
    public void Parses_logs_merging_streams_by_timestamp()
    {
        var logs = DockerOutputParser.ParseLogs("web", DockerSamples.LogsStdout, DockerSamples.LogsStderr, 200);

        Assert.False(logs.Truncated);
        Assert.Equal(
            ["/docker-entrypoint.sh: Configuration complete", "follow-test-stdout-1", "follow-test-stderr-2", "renkli satır"],
            logs.Lines.Select(l => l.Text));
        Assert.True(logs.Lines[2].IsError);
        Assert.False(logs.Lines[1].IsError);
        Assert.Equal("2026-10-03T18:48:19.219917720Z", logs.Lines[^1].RawTimestamp);
    }

    [Fact]
    public void Log_tail_keeps_latest_lines()
    {
        var logs = DockerOutputParser.ParseLogs("web", DockerSamples.LogsStdout, DockerSamples.LogsStderr, 2);

        Assert.True(logs.Truncated);
        Assert.Equal(["follow-test-stderr-2", "renkli satır"], logs.Lines.Select(l => l.Text));
    }

    [Fact]
    public void Log_lines_without_timestamp_are_kept_and_long_lines_are_truncated()
    {
        var longLine = new string('x', DockerOutputParser.MaxLogLineLength + 50);
        var logs = DockerOutputParser.ParseLogs("web", "plain line\r\n" + longLine + "\n", string.Empty, 10);

        Assert.Equal("plain line", logs.Lines[0].Text);
        Assert.Null(logs.Lines[0].Timestamp);
        Assert.Null(logs.Lines[0].RawTimestamp);
        Assert.Equal(DockerOutputParser.MaxLogLineLength + 1, logs.Lines[1].Text.Length);
        Assert.EndsWith("…", logs.Lines[1].Text);
    }

    [Fact]
    public void Json_lines_ignore_garbage()
    {
        var rows = DockerOutputParser.ParseJsonLines("WARNING: x\n{\"a\":1}\n{broken\n\n{\"b\":2}");

        Assert.Equal(2, rows.Count);
    }
}

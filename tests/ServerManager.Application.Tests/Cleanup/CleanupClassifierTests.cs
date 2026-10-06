using ServerManager.Application.Cleanup;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Tests.Cleanup;

public class CleanupClassifierTests
{
    private static readonly IReadOnlyDictionary<string, string> NoLabels = new Dictionary<string, string>();

    private static readonly PanelResourceContext Context = new(
        new Dictionary<string, PanelResourceRef> { ["shop"] = new(Guid.NewGuid(), "Shop API") },
        new Dictionary<string, PanelResourceRef> { ["db"] = new(Guid.NewGuid(), "Postgres") });

    private static Dictionary<string, string> Labels(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static IReadOnlyList<CleanupItem> Items(CleanupFacts facts, CleanupCategory category, CleanupOptions? options = null) =>
        CleanupClassifier.Classify(facts, Context, options ?? new CleanupOptions()).Single(g => g.Category == category).Items;

    [Fact]
    public void Panel_networks_and_docker_defaults_are_never_listed()
    {
        var facts = new CleanupFacts
        {
            DockerAvailable = true,
            UnusedNetworks =
            [
                new DockerNetworkFact("n1", "sm-proxy", "bridge", NoLabels),
                new DockerNetworkFact("n2", "sm-services", "bridge", NoLabels),
                new DockerNetworkFact("n3", "bridge", "bridge", NoLabels),
                new DockerNetworkFact("n4", "host", "host", NoLabels),
                new DockerNetworkFact("n5", "none", "null", NoLabels),
                new DockerNetworkFact("n6", "docker_gwbridge", "bridge", NoLabels),
                new DockerNetworkFact("n7", "appnet", "bridge", NoLabels)
            ]
        };

        var items = Items(facts, CleanupCategory.Networks);

        var item = Assert.Single(items);
        Assert.Equal("appnet", item.Name);
        Assert.Equal(CleanupSafety.Safe, item.Safety);
        Assert.True(item.Preselected);
        Assert.DoesNotContain(items, i => i.Target is "n1" or "n2");
    }

    [Fact]
    public void Compose_network_of_panel_project_is_caution()
    {
        var facts = new CleanupFacts
        {
            DockerAvailable = true,
            UnusedNetworks = [new DockerNetworkFact("n1", "sm-shop_default", "bridge", Labels(("com.docker.compose.project", "sm-shop")))]
        };

        var item = Assert.Single(Items(facts, CleanupCategory.Networks));

        Assert.Equal(CleanupSafety.Caution, item.Safety);
        Assert.False(item.Preselected);
        Assert.Contains("Panel projesine ait", item.Reason, StringComparison.Ordinal);
        Assert.Contains("Shop API", item.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Volumes_are_never_preselected_and_managed_service_data_is_caution()
    {
        var anonymous = new string('a', 64);
        var facts = new CleanupFacts
        {
            DockerAvailable = true,
            Volumes =
            [
                new DockerVolumeFact("sm-svc-db-data", 0, 1_000_000, NoLabels),
                new DockerVolumeFact("sm-svc-old-data", 0, 500, Labels(("sm.managed", "true"))),
                new DockerVolumeFact("shop_pgdata", 0, 2_000, Labels(("com.docker.compose.project", "shop"))),
                new DockerVolumeFact("orphan-vol", 0, 10, NoLabels),
                new DockerVolumeFact(anonymous, 0, 20, NoLabels),
                new DockerVolumeFact("in-use", 1, 30, NoLabels),
                new DockerVolumeFact("unknown-links", null, 30, NoLabels)
            ]
        };

        var items = Items(facts, CleanupCategory.Volumes);

        Assert.All(items, i => Assert.False(i.Preselected));
        Assert.DoesNotContain(items, i => i.Target is "in-use" or "unknown-links");

        var service = items.Single(i => i.Target == "sm-svc-db-data");
        Assert.Equal(CleanupSafety.Caution, service.Safety);
        Assert.Contains("Panel servisi: Postgres", service.Reason, StringComparison.Ordinal);
        Assert.Equal(CleanupSafety.Caution, items.Single(i => i.Target == "sm-svc-old-data").Safety);
        Assert.Contains("Compose projesi", items.Single(i => i.Target == "shop_pgdata").Reason, StringComparison.Ordinal);
        Assert.Equal(CleanupSafety.Caution, items.Single(i => i.Target == "orphan-vol").Safety);
        Assert.Equal(CleanupSafety.Safe, items.Single(i => i.Target == anonymous).Safety);
    }

    [Fact]
    public void Stopped_panel_containers_are_caution_and_others_are_preselected()
    {
        var facts = new CleanupFacts
        {
            DockerAvailable = true,
            Containers =
            [
                new DockerContainerFact("c1", "failed-job", "alpine:3", "exited", "Exited (3) 2 days ago", 10, NoLabels),
                new DockerContainerFact("c2", "sm-shop", "sm-shop:abc", "exited", "Exited (0)", 0, Labels(("sm.project", "shop"))),
                new DockerContainerFact("c3", "sm-svc-db", "postgres:17", "exited", "Exited (0)", 0, Labels(("sm.service", "db"), ("sm.managed", "true"))),
                new DockerContainerFact("c4", "sm-other_web_1", "nginx", "created", "Created", 0, Labels(("com.docker.compose.project", "sm-other"))),
                new DockerContainerFact("c5", "web", "nginx", "running", "Up 2 hours", 0, NoLabels)
            ]
        };

        var items = Items(facts, CleanupCategory.Containers);

        Assert.Equal(["c1", "c2", "c3", "c4"], items.Select(i => i.Target));
        var plain = items.Single(i => i.Target == "c1");
        Assert.Equal(CleanupSafety.Safe, plain.Safety);
        Assert.True(plain.Preselected);
        Assert.All(items.Where(i => i.Target != "c1"), i =>
        {
            Assert.Equal(CleanupSafety.Caution, i.Safety);
            Assert.False(i.Preselected);
            Assert.StartsWith("Panel projesine ait", i.Reason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Rollback_images_of_panel_projects_are_caution_and_used_images_are_skipped()
    {
        var facts = new CleanupFacts
        {
            DockerAvailable = true,
            Images =
            [
                new DockerImageFact("sha256:aaa", "<none>", "<none>", 100, 0, null),
                new DockerImageFact("sha256:bbb", "sm-shop", "1a2b3c4d5e6f", 200, 0, null),
                new DockerImageFact("sha256:ccc", "postgres", "16-alpine", 300, 0, null),
                new DockerImageFact("sha256:ccc", "postgres", "16", 300, 0, null),
                new DockerImageFact("sha256:ddd", "nginx", "alpine", 400, 1, null),
                new DockerImageFact("sha256:eee", "redis", "7", 400, -1, null),
                new DockerImageFact("sha256:fff", "<none>", "<none>", 50, 2, null)
            ]
        };

        var items = Items(facts, CleanupCategory.Images);

        Assert.DoesNotContain(items, i => i.Target is "nginx:alpine" or "redis:7" or "sha256:fff");
        var dangling = items.Single(i => i.Target == "sha256:aaa");
        Assert.True(dangling.Preselected);
        Assert.Equal(CleanupSafety.Safe, dangling.Safety);

        var rollback = items.Single(i => i.Target == "sm-shop:1a2b3c4d5e6f");
        Assert.Equal(CleanupSafety.Caution, rollback.Safety);
        Assert.False(rollback.Preselected);
        Assert.Contains("KeepImageCount", rollback.Reason, StringComparison.Ordinal);

        // Aynı imajın ikinci etiketi boyutu yeniden saymaz.
        Assert.Equal(300, items.Where(i => i.Target.StartsWith("postgres:", StringComparison.Ordinal)).Sum(i => i.SizeBytes ?? 0));
    }

    [Fact]
    public void Docker_groups_are_omitted_when_docker_is_missing()
    {
        var groups = CleanupClassifier.Classify(new CleanupFacts { DockerAvailable = false }, Context, new CleanupOptions());

        Assert.DoesNotContain(groups, g => g.Category is CleanupCategory.Containers or CleanupCategory.Images or CleanupCategory.Volumes);
        Assert.Contains(groups, g => g.Category == CleanupCategory.PackageCache);
    }

    [Fact]
    public void Journal_reclaimable_size_is_usage_above_target()
    {
        var facts = new CleanupFacts { JournalAvailable = true, JournalBytes = 500L * 1024 * 1024 };

        var item = Assert.Single(Items(facts, CleanupCategory.Journal, new CleanupOptions { JournalMaxMegabytes = 200 }));
        var under = Assert.Single(Items(facts, CleanupCategory.Journal, new CleanupOptions { JournalMaxMegabytes = 800 }));

        Assert.Equal(300L * 1024 * 1024, item.SizeBytes);
        Assert.True(item.Preselected);
        Assert.Equal(0, under.SizeBytes);
        Assert.False(under.Preselected);
    }

    [Fact]
    public void Temp_files_are_caution_and_rotated_logs_are_safe()
    {
        var files = new FileSetFact(3, 3000, []);
        var facts = new CleanupFacts { RotatedLogs = files, TempFiles = files };

        var logs = Assert.Single(Items(facts, CleanupCategory.RotatedLogs));
        var tmp = Assert.Single(Items(facts, CleanupCategory.TempFiles));

        Assert.Equal(CleanupSafety.Safe, logs.Safety);
        Assert.True(logs.Preselected);
        Assert.Equal(CleanupSafety.Caution, tmp.Safety);
        Assert.False(tmp.Preselected);
    }

    [Fact]
    public void Old_kernels_are_shown_but_not_deletable_and_running_kernel_is_hidden()
    {
        var facts = new CleanupFacts
        {
            PackageManager = "apt",
            RunningKernel = "6.8.0-45-generic",
            Kernels =
            [
                new KernelFact("linux-image-6.8.0-40-generic", "6.8.0-40-generic", 100),
                new KernelFact("linux-image-6.8.0-45-generic", "6.8.0-45-generic", 100)
            ]
        };

        var group = CleanupClassifier.Classify(facts, Context, new CleanupOptions()).Single(g => g.Category == CleanupCategory.Kernels);

        var item = Assert.Single(group.Items);
        Assert.Equal("linux-image-6.8.0-40-generic", item.Target);
        Assert.False(item.CanDelete);
        Assert.False(item.Preselected);
        Assert.Equal(0, group.TotalBytes);
        Assert.Contains("apt autoremove", group.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_are_clamped_to_safe_ranges()
    {
        var options = CleanupRules.Normalize(new CleanupOptions { LogDays = 0, TempDays = 99999, JournalMaxMegabytes = 1 });

        Assert.Equal(CleanupOptions.DefaultLogDays, options.LogDays);
        Assert.Equal(CleanupRules.MaxDays, options.TempDays);
        Assert.Equal(CleanupRules.MinJournalMegabytes, options.JournalMaxMegabytes);
    }

    [Theory]
    [InlineData("container:3a06297ea27b", true)]
    [InlineData("image:sha256:594ccb1a1198", true)]
    [InlineData("image:registry.local:5000/app:1.0", true)]
    [InlineData("snap:core20|2182", true)]
    [InlineData("journal", true)]
    [InlineData("volume:x; rm -rf /", false)]
    [InlineData("image:$(id)", false)]
    [InlineData("", false)]
    public void Key_format_rejects_shell_characters(string key, bool valid) =>
        Assert.Equal(valid, CleanupRules.IsValidKey(key));
}

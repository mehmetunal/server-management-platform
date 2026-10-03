using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Tests.Core;

public class DokployCompatibilityEvaluatorTests
{
    private static readonly DateTime CheckedAt = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DokployOptions Options = new();

    private static DokployHostFacts Facts(
        string? osId = "ubuntu",
        string? kernel = "Linux",
        string? arch = "x86_64",
        int? userId = 0,
        string? container = "none",
        long? memoryKb = 4L * 1024 * 1024,
        long? diskKb = 80L * 1024 * 1024,
        bool useSudo = false,
        bool? sudoWorks = null,
        bool dockerInstalled = true,
        string? dockerVersion = "28.5.0",
        string? swarm = "inactive",
        bool dokployExists = false,
        IReadOnlyList<int>? ports = null,
        bool portToolMissing = false,
        bool curl = true,
        bool scriptReachable = true,
        bool registryReachable = true) => new()
    {
        OsId = osId,
        OsName = osId is null ? null : $"{osId} test",
        Kernel = kernel,
        Architecture = arch,
        UserId = userId,
        ContainerKind = container,
        MemoryKb = memoryKb,
        DiskAvailableKb = diskKb,
        CurlAvailable = curl,
        BashAvailable = true,
        UseSudo = useSudo,
        SudoWorks = sudoWorks,
        DockerInstalled = dockerInstalled,
        DockerVersion = dockerVersion,
        SwarmState = swarm,
        DokployServiceExists = dokployExists,
        ListeningPorts = portToolMissing ? null : ports ?? [22],
        ScriptReachable = scriptReachable,
        RegistryReachable = registryReachable
    };

    private static DokployCompatibilityCheckDto Check(DokployCompatibilityReportDto report, string key) =>
        report.Checks.Single(c => c.Key == key);

    [Fact]
    public void Healthy_root_server_passes_every_check()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(), Options, CheckedAt);

        Assert.True(report.CanInstall);
        Assert.False(report.HasWarnings);
        Assert.True(report.IsRoot);
        Assert.Equal(CheckedAt, report.CheckedAt);
        Assert.Equal(["os", "arch", "container", "privileges", "memory", "disk", "docker", "swarm", "ports", "internet"], report.Checks.Select(c => c.Key));
    }

    [Fact]
    public void Running_inside_docker_blocks_installation()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(container: "docker"), Options, CheckedAt);

        Assert.False(report.CanInstall);
        Assert.Equal(DokployCheckStatus.Failed, Check(report, "container").Status);
    }

    [Fact]
    public void Lxc_and_unknown_distribution_are_warnings()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(container: "lxc", osId: "alpine"), Options, CheckedAt);

        Assert.True(report.CanInstall);
        Assert.True(report.HasWarnings);
        Assert.Equal(DokployCheckStatus.Warning, Check(report, "container").Status);
        Assert.Equal(DokployCheckStatus.Warning, Check(report, "os").Status);
    }

    [Fact]
    public void Non_linux_kernel_fails()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(kernel: "Darwin"), Options, CheckedAt);

        Assert.Equal(DokployCheckStatus.Failed, Check(report, "os").Status);
    }

    [Theory]
    [InlineData(false, null, DokployCheckStatus.Failed)]
    [InlineData(true, false, DokployCheckStatus.Failed)]
    [InlineData(true, true, DokployCheckStatus.Passed)]
    public void Non_root_user_needs_working_sudo(bool useSudo, bool? sudoWorks, DokployCheckStatus expected)
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(userId: 1000, useSudo: useSudo, sudoWorks: sudoWorks), Options, CheckedAt);

        Assert.Equal(expected, Check(report, "privileges").Status);
        Assert.False(report.IsRoot);
    }

    [Theory]
    [InlineData(1024L * 1024, DokployCheckStatus.Failed)]
    [InlineData(2_000_000L, DokployCheckStatus.Passed)]
    public void Memory_below_minimum_fails_with_small_tolerance(long memoryKb, DokployCheckStatus expected)
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(memoryKb: memoryKb), Options, CheckedAt);

        Assert.Equal(expected, Check(report, "memory").Status);
    }

    [Fact]
    public void Low_disk_is_only_a_warning()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(diskKb: 10L * 1024 * 1024), Options, CheckedAt);

        Assert.True(report.CanInstall);
        Assert.Equal(DokployCheckStatus.Warning, Check(report, "disk").Status);
    }

    [Fact]
    public void Missing_docker_is_a_warning_because_script_installs_it()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(dockerInstalled: false, dockerVersion: null), Options, CheckedAt);

        Assert.True(report.CanInstall);
        Assert.Equal(DokployCheckStatus.Warning, Check(report, "docker").Status);
    }

    [Theory]
    [InlineData("active", false)]
    [InlineData("inactive", true)]
    public void Existing_swarm_blocks_installation(string swarm, bool canInstall)
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(swarm: swarm), Options, CheckedAt);

        Assert.Equal(canInstall, report.CanInstall);
    }

    [Fact]
    public void Existing_dokploy_blocks_installation()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(dokployExists: true, swarm: "active"), Options, CheckedAt);

        var swarm = Check(report, "swarm");
        Assert.Equal(DokployCheckStatus.Failed, swarm.Status);
        Assert.Contains("zaten kurulu", swarm.Detail);
    }

    [Fact]
    public void Busy_required_port_is_reported()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(ports: [22, 80, 8080]), Options, CheckedAt);

        var ports = Check(report, "ports");
        Assert.Equal(DokployCheckStatus.Failed, ports.Status);
        Assert.Contains("80", ports.Detail);
        Assert.DoesNotContain("8080", ports.Detail);
    }

    [Fact]
    public void Unknown_ports_are_a_warning()
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(portToolMissing: true), Options, CheckedAt);

        Assert.Equal(DokployCheckStatus.Warning, Check(report, "ports").Status);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Internet_requires_curl_script_and_registry(bool curl, bool script, bool registry)
    {
        var report = DokployCompatibilityEvaluator.Evaluate(Facts(curl: curl, scriptReachable: script, registryReachable: registry), Options, CheckedAt);

        Assert.Equal(DokployCheckStatus.Failed, Check(report, "internet").Status);
        Assert.False(report.CanInstall);
    }
}

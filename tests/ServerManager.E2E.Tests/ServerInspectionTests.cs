using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Cleanup;
using ServerManager.Application.Interfaces.Services;
using ServerManager.E2E.Tests.Infrastructure;

namespace ServerManager.E2E.Tests;

/// <summary>Docker genel bakışı, kaynak kullanımı ve sunucu temizliği taraması — gerçek komut çıktılarının ayrıştırılması.</summary>
[Collection(E2ECollection.Name)]
public sealed class ServerInspectionTests(E2EFixture fixture)
{
    private const string ProbeImage = "busybox:1.36";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Docker_overview_reports_engine_and_counts()
    {
        await fixture.RequireAsync();
        var container = E2EFixture.Unique("e2e-overview");
        await fixture.Shell.RunCheckedAsync($"docker pull -q {ProbeImage} && docker create --name {container} {ProbeImage} true", TimeSpan.FromMinutes(5), Ct);
        try
        {
            var overview = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IDockerService>().GetOverviewAsync(fixture.ServerId, Ct));
            Assert.True(overview.IsSuccess, overview.Message);
            var data = overview.Data!;
            Assert.False(string.IsNullOrWhiteSpace(data.EngineVersion));
            Assert.True(data.CpuCount > 0);
            Assert.True(data.MemoryTotalBytes > 0);
            Assert.True(data.Images >= 1);
            Assert.True(data.ContainersTotal >= 1);
            Assert.True(data.ContainersStopped >= 1);

            var containers = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IDockerService>().GetContainersAsync(fixture.ServerId, Ct));
            Assert.True(containers.IsSuccess, containers.Message);
            Assert.Contains(containers.Data!, c => c.Name.Contains(container, StringComparison.Ordinal));
        }
        finally
        {
            await fixture.Shell.TryRunAsync($"docker rm -f {container}");
        }
    }

    [Fact]
    public async Task Resource_usage_snapshot_has_sane_values()
    {
        await fixture.RequireAsync();

        var result = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IResourceUsageService>().GetOverviewAsync(fixture.ServerId, includeDocker: true, Ct));
        Assert.True(result.IsSuccess, result.Message);
        var overview = result.Data!;
        var snapshot = overview.Snapshot;

        Assert.True(snapshot.CpuCores is > 0, $"CpuCores: {snapshot.CpuCores}");
        Assert.True(snapshot.Load1 is >= 0, $"Load1: {snapshot.Load1}");
        Assert.True(snapshot.UptimeSeconds is > 0, $"Uptime: {snapshot.UptimeSeconds}");
        Assert.NotNull(snapshot.Memory);
        Assert.True(snapshot.Memory.TotalKilobytes > 0);
        Assert.InRange(snapshot.Memory.UsedPercent, 0, 100);
        if (snapshot.Cpu is { } cpu)
        {
            Assert.InRange(cpu.Idle, 0, 100);
            Assert.InRange(cpu.Busy, 0, 100);
        }

        Assert.NotNull(overview.Processes);
        Assert.NotEmpty(overview.TopCpu(5));
        Assert.True(overview.DockerIncluded);
        Assert.NotNull(overview.Containers);

        var disk = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IResourceUsageService>().ScanDiskAsync(fixture.ServerId, "/var/log", Ct));
        Assert.True(disk.IsSuccess, disk.Message);
    }

    [Fact]
    public async Task Cleanup_scan_finds_stopped_container_and_preview_does_not_delete_it()
    {
        await fixture.RequireAsync();
        var container = E2EFixture.Unique("e2e-cleanup");
        await fixture.Shell.RunCheckedAsync($"docker pull -q {ProbeImage} && docker create --name {container} {ProbeImage} true", TimeSpan.FromMinutes(5), Ct);
        try
        {
            var options = new CleanupOptions();
            var scan = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerCleanupService>().ScanAsync(fixture.ServerId, options, Ct));
            Assert.True(scan.IsSuccess, scan.Message);
            Assert.True(scan.Data!.DockerAvailable, scan.Data.DockerMessage);
            Assert.NotEmpty(scan.Data.Groups);

            var item = Assert.Single(scan.Data.Items, i => i.Category == CleanupCategory.Containers && i.Name.Contains(container, StringComparison.Ordinal));
            Assert.True(item.CanDelete);

            var logs = new List<CleanupLogEntry>();
            var preview = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IServerCleanupService>().ExecuteAsync(
                fixture.ServerId,
                [item.Key],
                options,
                dryRun: true,
                (entry, _) =>
                {
                    lock (logs)
                        logs.Add(entry);
                    return Task.CompletedTask;
                },
                Ct));
            Assert.True(preview.IsSuccess, preview.Message);
            Assert.True(preview.Data!.DryRun);
            Assert.Equal(0, preview.Data.Failed);
            Assert.Null(preview.Data.FreedBytes);
            Assert.NotEmpty(logs);

            // Önizleme hiçbir şeyi silmemeli.
            var exists = await fixture.Shell.RunAsync($"docker container inspect {container}", cancellationToken: Ct);
            Assert.Equal(0, exists.ExitCode);
        }
        finally
        {
            await fixture.Shell.TryRunAsync($"docker rm -f {container}");
        }
    }
}

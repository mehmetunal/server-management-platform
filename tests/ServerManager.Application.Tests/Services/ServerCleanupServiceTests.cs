using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Tests.Services;

public class ServerCleanupServiceTests
{
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IServerCleanupInspector _inspector = Substitute.For<IServerCleanupInspector>();
    private readonly IDeploymentRepository _deployments = Substitute.For<IDeploymentRepository>();
    private readonly IManagedServiceRepository _services = Substitute.For<IManagedServiceRepository>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "10.0.0.5", Username = "deploy" }, UseSudo = true };
    private readonly ServerCleanupService _service;
    private readonly List<CleanupLogEntry> _log = [];

    private static readonly IReadOnlyDictionary<string, string> NoLabels = new Dictionary<string, string>();

    public ServerCleanupServiceTests()
    {
        _connectionProvider.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _serverId, ServerName = "web-01", Context = _context }));
        _deployments.GetProjectsByServerAsync(_serverId, Arg.Any<CancellationToken>()).Returns(new List<DeploymentProject>());
        _services.ListAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(new List<ManagedService> { new() { ServerId = _serverId, Name = "Postgres", Slug = "db" } });
        _inspector.ScanAsync(_context, Arg.Any<CleanupOptions>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<CleanupFacts>.Success(new CleanupFacts
            {
                DockerAvailable = true,
                UnusedNetworks = [new DockerNetworkFact("n-app", "appnet", "bridge", NoLabels), new DockerNetworkFact("n-proxy", "sm-proxy", "bridge", NoLabels)],
                Volumes = [new DockerVolumeFact("sm-svc-db-data", 0, 1000, NoLabels)],
                Kernels = [new KernelFact("linux-image-6.1.0-1-amd64", "6.1.0-1-amd64", 10)],
                RunningKernel = "6.1.0-9-amd64"
            }));
        _inspector.ExecuteAsync(_context, Arg.Any<IReadOnlyList<CleanupItem>>(), Arg.Any<CleanupOptions>(), Arg.Any<bool>(),
                Arg.Any<Func<CleanupLogEntry, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var items = call.ArgAt<IReadOnlyList<CleanupItem>>(1);
                return ServiceResult<CleanupRunResult>.Success(new CleanupRunResult(call.ArgAt<bool>(3), items.Count, 0, 0, 4096, 0, items.Select(i => i.Name).ToList()));
            });

        _service = new ServerCleanupService(_connectionProvider, _inspector, _deployments, _services, _auditLog);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task Collect(CleanupLogEntry entry, CancellationToken _)
    {
        _log.Add(entry);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Execute_only_processes_keys_present_in_a_fresh_scan()
    {
        var result = await _service.ExecuteAsync(_serverId,
            ["network:n-app", "network:n-proxy", "volume:sm-svc-db-data", "kernel:linux-image-6.1.0-1-amd64", "container:gone"],
            new CleanupOptions(), dryRun: false, Collect, Ct);

        Assert.True(result.IsSuccess);
        await _inspector.Received(1).ExecuteAsync(_context,
            Arg.Is<IReadOnlyList<CleanupItem>>(items => items.Select(i => i.Target).SequenceEqual(new[] { "n-app", "sm-svc-db-data" })),
            Arg.Any<CleanupOptions>(), false, Arg.Any<Func<CleanupLogEntry, CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        Assert.Equal(3, result.Data!.Skipped);
        Assert.Contains(_log, e => e.Level == CleanupLogLevel.Warning);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ServerCleanup
                                    && e.EntityId == _serverId.ToString()
                                    && e.IsSuccess
                                    && e.Details!.Contains("Kazanılan alan: 4 KB (4096 bayt)")
                                    && e.Details.Contains("appnet")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dry_run_is_not_audited()
    {
        var result = await _service.ExecuteAsync(_serverId, ["network:n-app"], new CleanupOptions(), dryRun: true, Collect, Ct);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("Önizleme tamamlandı", result.Message, StringComparison.Ordinal);
        await _auditLog.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_fails_when_nothing_selectable_remains()
    {
        var result = await _service.ExecuteAsync(_serverId, ["network:n-proxy", "bad key; rm"], new CleanupOptions(), dryRun: false, Collect, Ct);

        Assert.False(result.IsSuccess);
        await _inspector.DidNotReceive().ExecuteAsync(Arg.Any<RemoteExecutionContext>(), Arg.Any<IReadOnlyList<CleanupItem>>(), Arg.Any<CleanupOptions>(),
            Arg.Any<bool>(), Arg.Any<Func<CleanupLogEntry, CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_rejects_empty_selection_without_connecting()
    {
        var result = await _service.ExecuteAsync(_serverId, [], new CleanupOptions(), dryRun: false, Collect, Ct);

        Assert.False(result.IsSuccess);
        await _connectionProvider.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scan_marks_managed_service_volume_with_service_name()
    {
        var result = await _service.ScanAsync(_serverId, new CleanupOptions(), Ct);

        Assert.True(result.IsSuccess);
        var volume = result.Data!.Items.Single(i => i.Category == CleanupCategory.Volumes);
        Assert.False(volume.Preselected);
        Assert.Contains("Panel servisi: Postgres", volume.Reason, StringComparison.Ordinal);
        Assert.True(result.Data.UsesSudo);
        Assert.DoesNotContain(result.Data.Items, i => i.Target == "n-proxy");
    }
}

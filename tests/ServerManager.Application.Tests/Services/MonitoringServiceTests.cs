using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class MonitoringServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 30, 0, TimeSpan.Zero);

    private readonly IServerRepository _serverRepository = Substitute.For<IServerRepository>();
    private readonly IServerMetricRepository _metricRepository = Substitute.For<IServerMetricRepository>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IMetricsCollector _collector = Substitute.For<IMetricsCollector>();
    private readonly IMonitoringNotifier _notifier = Substitute.For<IMonitoringNotifier>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly RetentionOptions _retention = new();
    private readonly MonitoringService _service;

    public MonitoringServiceTests()
    {
        _service = new MonitoringService(
            _serverRepository,
            _metricRepository,
            _protector,
            _collector,
            _notifier,
            _auditLog,
            new FixedTimeProvider(Now),
            Options.Create(new MonitoringOptions()),
            Options.Create(_retention),
            NullLogger<MonitoringService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Server CreateServer(ServerStatus status = ServerStatus.Healthy, string? fingerprint = "SHA256:abc")
    {
        var server = new Server
        {
            Id = Guid.NewGuid(),
            Name = "web-01",
            Hostname = "web-01.local",
            IpAddress = "10.0.0.5",
            SshPort = 22,
            Username = "deploy",
            AuthenticationType = AuthenticationType.Password,
            Status = status,
            HostKeyFingerprint = fingerprint,
            MonitoringEnabled = true
        };
        server.Credential = new ServerCredential { ServerId = server.Id, EncryptedPassword = _protector.Protect("secret") };
        _serverRepository.GetWithDetailsAsync(server.Id, Arg.Any<CancellationToken>()).Returns(server);
        return server;
    }

    private static SystemMetricsSnapshot Snapshot(double cpu = 20, double disk = 40) => new()
    {
        CpuUsagePercent = cpu,
        MemoryTotalBytes = 1000,
        MemoryAvailableBytes = 600,
        Disks = [new DiskUsageInfo { MountPoint = "/", TotalBytes = 100, UsedBytes = (long)disk, UsagePercent = disk }],
        NetworkInterfaces =
        [
            new NetworkInterfaceInfo { Name = "eth0", RxBytesPerSecond = 100, TxBytesPerSecond = 50 },
            new NetworkInterfaceInfo { Name = "docker0", IsVirtual = true, RxBytesPerSecond = 999, TxBytesPerSecond = 999 }
        ],
        System = new SystemInfo { UptimeSeconds = 3600, OperatingSystem = "Ubuntu 24.04 LTS" }
    };

    private void CollectorReturns(MetricsCollectionResult result) =>
        _collector.CollectAsync(Arg.Any<SshConnectionRequest>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task Successful_collection_stores_metric_snapshot_and_health_check()
    {
        var server = CreateServer(ServerStatus.Unknown);
        server.ConsecutiveFailureCount = 1;
        CollectorReturns(new MetricsCollectionResult { IsSuccess = true, Message = "ok", DurationMs = 120, Snapshot = Snapshot() });
        ServerMetric? metric = null;
        await _metricRepository.AddMetricAsync(Arg.Do<ServerMetric>(m => metric = m), Arg.Any<CancellationToken>());

        var result = await _service.CollectAsync(server.Id, manual: false, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ServerStatus.Healthy, server.Status);
        Assert.Equal(Now.UtcDateTime, server.LastSeenAt);
        Assert.Equal(0, server.ConsecutiveFailureCount);
        Assert.Equal("Ubuntu 24.04 LTS", server.OperatingSystem);
        Assert.NotNull(metric);
        Assert.Equal(40, metric.MemoryUsagePercent);
        Assert.Equal(100, metric.NetworkRxBytesPerSecond);
        Assert.Equal(50, metric.NetworkTxBytesPerSecond);
        await _collector.Received(1).CollectAsync(
            Arg.Is<SshConnectionRequest>(r => r.Password == "secret" && r.ExpectedHostKeyFingerprint == "SHA256:abc"),
            Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).UpsertSnapshotAsync(server.Id, Arg.Any<SystemMetricsSnapshot>(), Now.UtcDateTime, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).AddHealthCheckAsync(
            Arg.Is<ServerHealthCheck>(h => h.IsSuccess && h.ResponseTimeMs == 120), Arg.Any<CancellationToken>());
        await _serverRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).ServerUpdatedAsync(Arg.Is<ServerMonitoringUpdateDto>(u => u.Latest != null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_change_is_audited_as_system_for_automatic_collection()
    {
        var server = CreateServer(ServerStatus.Healthy);
        CollectorReturns(new MetricsCollectionResult { IsSuccess = true, Message = "ok", Snapshot = Snapshot(cpu: 97) });

        await _service.CollectAsync(server.Id, manual: false, Ct);

        Assert.Equal(ServerStatus.Critical, server.Status);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e =>
                e.Action == AuditActions.ServerStatusChanged
                && e.UserNameOverride == MonitoringService.SystemUserName
                && !e.IsSuccess
                && e.Details!.Contains("Healthy -> Critical")
                && e.Details.Contains("CPU %97")),
            Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ServerMetricsCollect), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unchanged_status_is_not_audited()
    {
        var server = CreateServer(ServerStatus.Healthy);
        CollectorReturns(new MetricsCollectionResult { IsSuccess = true, Message = "ok", Snapshot = Snapshot() });

        await _service.CollectAsync(server.Id, manual: false, Ct);

        await _auditLog.DidNotReceive().LogAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Server_goes_offline_after_consecutive_failures()
    {
        var server = CreateServer(ServerStatus.Healthy);
        CollectorReturns(new MetricsCollectionResult { IsSuccess = false, Message = "Bağlantı zaman aşımına uğradı." });

        await _service.CollectAsync(server.Id, manual: false, Ct);
        Assert.Equal(ServerStatus.Healthy, server.Status);
        Assert.Equal(1, server.ConsecutiveFailureCount);

        await _service.CollectAsync(server.Id, manual: false, Ct);
        Assert.Equal(ServerStatus.Offline, server.Status);
        Assert.Equal(2, server.ConsecutiveFailureCount);

        await _metricRepository.DidNotReceive().AddMetricAsync(Arg.Any<ServerMetric>(), Arg.Any<CancellationToken>());
        await _metricRepository.Received(2).AddHealthCheckAsync(Arg.Is<ServerHealthCheck>(h => !h.IsSuccess), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fingerprint_mismatch_marks_server_offline_immediately()
    {
        var server = CreateServer(ServerStatus.Healthy);
        CollectorReturns(new MetricsCollectionResult { IsSuccess = false, FingerprintMismatch = true, Message = "mismatch" });

        await _service.CollectAsync(server.Id, manual: false, Ct);

        Assert.Equal(ServerStatus.Offline, server.Status);
        Assert.Equal("SHA256:abc", server.HostKeyFingerprint);
    }

    [Fact]
    public async Task Maintenance_status_is_never_changed()
    {
        var server = CreateServer(ServerStatus.Maintenance);
        CollectorReturns(new MetricsCollectionResult { IsSuccess = true, Message = "ok", Snapshot = Snapshot(cpu: 99) });

        await _service.CollectAsync(server.Id, manual: false, Ct);
        Assert.Equal(ServerStatus.Maintenance, server.Status);

        CollectorReturns(new MetricsCollectionResult { IsSuccess = false, FingerprintMismatch = true, Message = "mismatch" });
        await _service.CollectAsync(server.Id, manual: false, Ct);
        Assert.Equal(ServerStatus.Maintenance, server.Status);
    }

    [Fact]
    public async Task Collection_requires_trusted_host_key()
    {
        var server = CreateServer(fingerprint: null);

        var result = await _service.CollectAsync(server.Id, manual: true, Ct);

        Assert.False(result.IsSuccess);
        await _collector.DidNotReceive().CollectAsync(Arg.Any<SshConnectionRequest>(), Arg.Any<CancellationToken>());
        await _serverRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Automatic_collection_skips_servers_with_monitoring_disabled()
    {
        var server = CreateServer();
        server.MonitoringEnabled = false;

        var result = await _service.CollectAsync(server.Id, manual: false, Ct);

        Assert.False(result.IsSuccess);
        await _collector.DidNotReceive().CollectAsync(Arg.Any<SshConnectionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manual_collection_is_audited_with_current_user()
    {
        var server = CreateServer();
        server.MonitoringEnabled = false;
        CollectorReturns(new MetricsCollectionResult { IsSuccess = true, Message = "Metrikler toplandı.", Snapshot = Snapshot() });

        var result = await _service.CollectAsync(server.Id, manual: true, Ct);

        Assert.True(result.IsSuccess);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ServerMetricsCollect && e.UserNameOverride == null && e.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Undecryptable_credentials_are_recorded_as_failed_check()
    {
        var server = CreateServer();
        _protector.FailOnUnprotect = true;

        var result = await _service.CollectAsync(server.Id, manual: false, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsSuccess);
        Assert.Equal(1, server.ConsecutiveFailureCount);
        await _collector.DidNotReceive().CollectAsync(Arg.Any<SshConnectionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notifier_failure_does_not_break_collection()
    {
        var server = CreateServer();
        CollectorReturns(new MetricsCollectionResult { IsSuccess = true, Message = "ok", Snapshot = Snapshot() });
        _notifier.ServerUpdatedAsync(Arg.Any<ServerMonitoringUpdateDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub down"));

        var result = await _service.CollectAsync(server.Id, manual: false, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unknown_server_returns_not_found()
    {
        var result = await _service.CollectAsync(Guid.NewGuid(), manual: true, Ct);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Maintenance_keeps_latest_records_and_applies_retention_windows()
    {
        var result = await _service.RunMaintenanceAsync(Ct);

        Assert.NotNull(result);
        await _metricRepository.Received(1).AggregateHourlyAsync(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc), Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).DeleteExpiredAsync(RetentionTarget.RawMetrics, Now.UtcDateTime.AddHours(-48), 1, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).DeleteExpiredAsync(RetentionTarget.HourlyMetrics, Now.UtcDateTime.AddDays(-90), 1, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).DeleteExpiredAsync(
            RetentionTarget.HealthChecks, Now.UtcDateTime.AddDays(-30), MonitoringService.ProtectedHealthChecksPerServer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Maintenance_applies_history_retention_and_skips_keep_forever_targets()
    {
        _retention.DeploymentLogDays = 30;
        _retention.CommandRunDays = 0;
        _metricRepository.DeleteExpiredAsync(RetentionTarget.DeploymentLogs, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(7);

        var result = await _service.RunMaintenanceAsync(Ct);

        await _metricRepository.Received(1).DeleteExpiredAsync(
            RetentionTarget.DeploymentLogs, Now.UtcDateTime.AddDays(-30), RetentionOptions.ProtectedDeploymentLogsPerProject, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).DeleteExpiredAsync(
            RetentionTarget.BackupRunLogs, Now.UtcDateTime.AddDays(-90), RetentionOptions.ProtectedBackupRunLogsPerJob, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).DeleteExpiredAsync(RetentionTarget.TerminalSessions, Now.UtcDateTime.AddDays(-90), 1, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).DeleteExpiredAsync(RetentionTarget.AlertEvents, Now.UtcDateTime.AddDays(-90), 1, Arg.Any<CancellationToken>());
        await _metricRepository.DidNotReceive().DeleteExpiredAsync(RetentionTarget.CommandRuns, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        Assert.Equal(7, result.History![RetentionTarget.DeploymentLogs]);
    }

    [Fact]
    public async Task Maintenance_continues_when_one_history_target_fails()
    {
        _metricRepository.DeleteExpiredAsync(RetentionTarget.DeploymentLogs, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("tablo kilitli"));

        await _service.RunMaintenanceAsync(Ct);

        await _metricRepository.Received(1).DeleteExpiredAsync(RetentionTarget.AlertEvents, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(30, true)]
    public void Retention_cutoff_is_null_for_keep_forever(int days, bool hasCutoff)
    {
        var now = Now.UtcDateTime;

        var cutoff = RetentionOptions.Cutoff(days, now);

        Assert.Equal(hasCutoff, cutoff.HasValue);
        if (hasCutoff)
            Assert.Equal(now.AddDays(-days), cutoff);
    }

    [Fact]
    public async Task Series_uses_hourly_table_for_long_ranges()
    {
        var server = CreateServer();
        _serverRepository.AnyAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Server, bool>>>(), Arg.Any<CancellationToken>()).Returns(true);

        await _service.GetSeriesAsync(server.Id, MetricRange.SevenDays, Ct);
        await _service.GetSeriesAsync(server.Id, MetricRange.OneHour, Ct);

        await _metricRepository.Received(1).GetHourlySeriesAsync(server.Id, Now.UtcDateTime.AddDays(-7), 3600, Arg.Any<CancellationToken>());
        await _metricRepository.Received(1).GetRawSeriesAsync(server.Id, Now.UtcDateTime.AddHours(-1), 30, Arg.Any<CancellationToken>());
    }
}

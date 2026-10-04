using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ServerManager.Application.Agent;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Tests.Services;

public class AgentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly IServerRepository _repository = Substitute.For<IServerRepository>();
    private readonly IMonitoringService _monitoring = Substitute.For<IMonitoringService>();
    private readonly IAgentReportParser _parser = Substitute.For<IAgentReportParser>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly AgentService _service;

    public AgentServiceTests()
    {
        _service = new AgentService(_repository, _monitoring, _parser, _auditLog, new FixedTimeProvider(Now), NullLogger<AgentService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Server ServerWithToken(out string token)
    {
        token = AgentTokens.Generate();
        var server = new Server { Name = "web-01", MonitoringEnabled = true, AgentTokenHash = AgentTokens.Hash(token) };
        _repository.GetByAgentTokenHashAsync(server.AgentTokenHash, Arg.Any<CancellationToken>()).Returns(server);
        _repository.GetByIdAsync(server.Id, Arg.Any<CancellationToken>()).Returns(server);
        return server;
    }

    [Fact]
    public async Task CreateToken_stores_only_hash_and_audits()
    {
        var server = new Server { Name = "web-01", AgentLastSeenAt = Now.UtcDateTime.AddDays(-1), AgentVersion = "0.9" };
        _repository.GetByIdAsync(server.Id, Arg.Any<CancellationToken>()).Returns(server);

        var result = await _service.CreateTokenAsync(server.Id, Ct);

        Assert.True(result.IsSuccess);
        var token = result.Data!.Token;
        Assert.True(AgentTokens.IsWellFormed(token));
        Assert.Equal(AgentTokens.Hash(token), server.AgentTokenHash);
        Assert.Equal(Now.UtcDateTime, server.AgentTokenCreatedAt);
        Assert.Null(server.AgentLastSeenAt);
        Assert.Null(server.AgentVersion);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.AgentTokenCreate && (e.Details == null || !e.Details.Contains(token))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateToken_replaces_existing_token()
    {
        var server = ServerWithToken(out var oldToken);

        var result = await _service.CreateTokenAsync(server.Id, Ct);

        Assert.NotEqual(AgentTokens.Hash(oldToken), server.AgentTokenHash);
        Assert.Contains("önceki token", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevokeToken_clears_hash_and_fails_without_token()
    {
        var server = ServerWithToken(out _);

        Assert.True((await _service.RevokeTokenAsync(server.Id, Ct)).IsSuccess);
        Assert.Null(server.AgentTokenHash);
        Assert.False((await _service.RevokeTokenAsync(server.Id, Ct)).IsSuccess);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.AgentTokenRevoke), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("sma_wrong")]
    public async Task Report_rejects_malformed_token(string? token)
    {
        var outcome = await _service.ReportAsync(token, "1.0.0", "x", Ct);

        Assert.Equal(AgentReportStatus.InvalidToken, outcome.Status);
        await _repository.DidNotReceive().GetByAgentTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_rejects_unknown_token()
    {
        var outcome = await _service.ReportAsync(AgentTokens.Generate(), "1.0.0", "x", Ct);

        Assert.Equal(AgentReportStatus.InvalidToken, outcome.Status);
    }

    [Fact]
    public async Task Report_rejects_when_monitoring_disabled()
    {
        var server = ServerWithToken(out var token);
        server.MonitoringEnabled = false;

        var outcome = await _service.ReportAsync(token, "1.0.0", "x", Ct);

        Assert.Equal(AgentReportStatus.MonitoringDisabled, outcome.Status);
    }

    [Fact]
    public async Task Report_rejects_too_frequent_reports()
    {
        var server = ServerWithToken(out var token);
        server.AgentLastSeenAt = Now.UtcDateTime.AddSeconds(-5);

        var outcome = await _service.ReportAsync(token, "1.0.0", "x", Ct);

        Assert.Equal(AgentReportStatus.TooFrequent, outcome.Status);
        _parser.DidNotReceive().Parse(Arg.Any<string>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Report_returns_invalid_report_when_parse_fails()
    {
        var server = ServerWithToken(out var token);
        _parser.Parse("bozuk", Arg.Any<DateTime>()).Throws(new FormatException("eksik"));

        var outcome = await _service.ReportAsync(token, "1.0.0", "bozuk", Ct);

        Assert.Equal(AgentReportStatus.InvalidReport, outcome.Status);
        Assert.Null(server.AgentLastSeenAt);
        await _monitoring.DidNotReceive().RecordAgentReportAsync(Arg.Any<Guid>(), Arg.Any<MetricsCollectionResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_records_snapshot_and_updates_agent_fields()
    {
        var server = ServerWithToken(out var token);
        var snapshot = new SystemMetricsSnapshot { CpuUsagePercent = 12.5 };
        _parser.Parse("ok", Now.UtcDateTime).Returns(snapshot);
        _monitoring.RecordAgentReportAsync(server.Id, Arg.Any<MetricsCollectionResult>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerMonitoringUpdateDto>.Success(new ServerMonitoringUpdateDto()));

        var outcome = await _service.ReportAsync(token, " 1.0.0 ", "ok", Ct);

        Assert.Equal(AgentReportStatus.Accepted, outcome.Status);
        Assert.Equal(Now.UtcDateTime, server.AgentLastSeenAt);
        Assert.Equal("1.0.0", server.AgentVersion);
        await _monitoring.Received(1).RecordAgentReportAsync(server.Id,
            Arg.Is<MetricsCollectionResult>(r => r.IsSuccess && r.Snapshot == snapshot), Arg.Any<CancellationToken>());
        await _repository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatus_reports_active_only_within_window()
    {
        var server = ServerWithToken(out _);
        server.AgentLastSeenAt = Now.UtcDateTime - AgentRules.ActiveWindow - TimeSpan.FromSeconds(1);

        var stale = await _service.GetStatusAsync(server.Id, Ct);
        server.AgentLastSeenAt = Now.UtcDateTime.AddSeconds(-30);
        var fresh = await _service.GetStatusAsync(server.Id, Ct);

        Assert.False(stale.Data!.IsActive);
        Assert.True(fresh.Data!.IsActive);
        Assert.True(fresh.Data.HasToken);
    }

    [Fact]
    public async Task MarkSilentAgentsOffline_records_failure_for_each_silent_server()
    {
        var silent = new Server { Name = "nat-01", AgentLastSeenAt = Now.UtcDateTime.AddMinutes(-12) };
        _repository.GetSilentAgentServersAsync(Now.UtcDateTime - AgentRules.SilentAfter, Arg.Any<CancellationToken>()).Returns([silent]);

        var count = await _service.MarkSilentAgentsOfflineAsync(Ct);

        Assert.Equal(1, count);
        await _monitoring.Received(1).RecordAgentReportAsync(silent.Id,
            Arg.Is<MetricsCollectionResult>(r => !r.IsSuccess && r.Message.Contains("12 dakikadır")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildInstallScript_uses_parser_collection_script()
    {
        _parser.CollectionScript.Returns("echo @@END");

        Assert.Contains("collect() {\necho @@END\n}", _service.BuildInstallScript());
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class TerminalServiceTests
{
    private const string ServerName = "web-01";

    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _serverId = Guid.NewGuid();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly ITerminalSessionFactory _sessionFactory = Substitute.For<ITerminalSessionFactory>();
    private readonly IDockerService _dockerService = Substitute.For<IDockerService>();
    private readonly ITerminalLogRepository _repository = Substitute.For<ITerminalLogRepository>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ITerminalOutputSink _sink = Substitute.For<ITerminalOutputSink>();
    private readonly ITerminalSession _terminal = Substitute.For<ITerminalSession>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "10.0.0.5", Username = "deploy" } };
    private readonly TerminalActor _actor = new(Guid.NewGuid(), "user-1", "ops@example.com", "10.1.1.1");
    private readonly TerminalService _service;

    public TerminalServiceTests()
    {
        _connectionProvider.GetAsync(_serverId, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _serverId, ServerName = ServerName, Context = _context }));
        _terminal.StartedAt.Returns(Now.UtcDateTime.AddMinutes(-42));

        _service = new TerminalService(
            _connectionProvider,
            _sessionFactory,
            _dockerService,
            _repository,
            _auditLog,
            new FixedTimeProvider(Now),
            NullLogger<TerminalService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Open_server_shell_clamps_size_audits_and_records_session()
    {
        _sessionFactory.OpenAsync(Arg.Any<TerminalOpenRequest>(), _sink, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ITerminalSession>.Success(_terminal));

        var result = await _service.OpenServerShellAsync(_serverId, 5, 1000, _actor, _sink, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(TerminalSessionKind.Server, result.Data!.Kind);
        await _sessionFactory.Received(1).OpenAsync(Arg.Is<TerminalOpenRequest>(r => r.Columns == 20 && r.Rows == 200 && r.Context == _context), _sink, Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.TerminalOpen && e.IsSuccess && e.UserIdOverride == "user-1" && e.IpAddressOverride == "10.1.1.1"),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(
            Arg.Is<TerminalSessionLog>(s => s.Id == _actor.SessionId && s.ServerId == _serverId && s.UserId == "user-1" && s.Kind == TerminalSessionKind.Server),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_open_is_audited_and_not_recorded()
    {
        _sessionFactory.OpenAsync(Arg.Any<TerminalOpenRequest>(), _sink, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ITerminalSession>.Failure("PTY açılamadı."));

        var result = await _service.OpenServerShellAsync(_serverId, 80, 24, _actor, _sink, Ct);

        Assert.False(result.IsSuccess);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.TerminalOpen && !e.IsSuccess && e.Details!.Contains("PTY açılamadı.")),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
    }

    [Fact]
    public async Task Record_commands_truncates_counts_executed_and_audits_dangerous()
    {
        var sessionId = _actor.SessionId;
        TerminalCommandEntry Entry(string text, TerminalCommandStatus status, string? rule = null) =>
            new(sessionId, _serverId, ServerName, _actor, Now.UtcDateTime, text, false, status, rule);

        await _service.RecordCommandsAsync(
        [
            Entry(new string('x', TerminalService.MaxCommandLength + 50), TerminalCommandStatus.Executed),
            Entry("rm -rf /srv/old", TerminalCommandStatus.Confirmed, "Özyinelemeli silme"),
            Entry("reboot", TerminalCommandStatus.Cancelled, "Sunucuyu yeniden başlatma")
        ], Ct);

        await _repository.Received(1).AddCommandsAsync(
            Arg.Is<IEnumerable<TerminalCommandLog>>(c => c.Count() == 3 && c.First().CommandText.Length == TerminalService.MaxCommandLength),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).IncrementCommandCountsAsync(
            Arg.Is<IReadOnlyDictionary<Guid, int>>(d => d.Count == 1 && d[sessionId] == 2),
            Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.TerminalDangerousCommand && e.IsSuccess && e.Details!.StartsWith("Onaylandı | Kural: Özyinelemeli silme")),
            Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.TerminalDangerousCommand && !e.IsSuccess && e.Details!.StartsWith("İptal edildi")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_session_closes_record_and_audits_duration()
    {
        var handle = new TerminalHandle { ServerId = _serverId, ServerName = ServerName, Kind = TerminalSessionKind.Container, Container = "web", Session = _terminal };

        await _service.CompleteSessionAsync(handle, _actor, "Kullanıcı kapattı", Ct);

        await _repository.Received(1).CloseSessionAsync(_actor.SessionId, Now.UtcDateTime, "Kullanıcı kapattı", Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DockerTerminalClose && e.Details == "Container: web, süre: 42 dk, neden: Kullanıcı kapattı"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Session_details_are_hidden_from_other_users_and_servers()
    {
        var sessionId = Guid.NewGuid();
        _repository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new TerminalSessionLog { Id = sessionId, ServerId = _serverId, UserId = "owner", ServerName = ServerName });

        var otherUser = await _service.GetSessionAsync(_serverId, sessionId, "someone-else", Ct);
        var otherServer = await _service.GetSessionAsync(Guid.NewGuid(), sessionId, null, Ct);
        var auditor = await _service.GetSessionAsync(_serverId, sessionId, null, Ct);

        Assert.Equal(ServiceErrorType.NotFound, otherUser.ErrorType);
        Assert.Equal(ServiceErrorType.NotFound, otherServer.ErrorType);
        Assert.True(auditor.IsSuccess);
    }
}

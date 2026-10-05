using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Commands;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Commands;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Commands;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Validators.Commands;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class CommandRunServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly ICommandRunRepository _repository = Substitute.For<ICommandRunRepository>();
    private readonly IServerGroupRepository _servers = Substitute.For<IServerGroupRepository>();
    private readonly IServerTemplateRepository _templates = Substitute.For<IServerTemplateRepository>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IServerScriptExecutor _executor = Substitute.For<IServerScriptExecutor>();
    private readonly Server _web = new() { Name = "web-1", IpAddress = "203.0.113.10" };
    private readonly Server _db = new() { Name = "db-1", IpAddress = "203.0.113.11" };
    private readonly CommandRunService _service;

    public CommandRunServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _currentUser.UserId.Returns("u1");
        var provider = new ServiceCollection().AddScoped(_ => _executor).BuildServiceProvider();
        _service = new CommandRunService(
            _repository,
            _servers,
            _templates,
            _auditLog,
            _currentUser,
            new CommandRunRequestDtoValidator(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task BeginAsync_RejectsEmptyCommandAndNoServers()
    {
        var result = await _service.BeginAsync(new CommandRunRequestDto { Command = "  " }, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CommandRunRequestDto.Command));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CommandRunRequestDto.ServerIds));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(901)]
    public async Task BeginAsync_RejectsTimeoutOutOfRange(int timeout)
    {
        var result = await _service.BeginAsync(new CommandRunRequestDto { Command = "uptime", ServerIds = [_web.Id], TimeoutSeconds = timeout }, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CommandRunRequestDto.TimeoutSeconds));
    }

    [Fact]
    public async Task BeginAsync_RejectsUnknownServer()
    {
        _servers.GetServersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([_web]);

        var result = await _service.BeginAsync(new CommandRunRequestDto { Command = "uptime", ServerIds = [_web.Id, Guid.NewGuid()] }, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CommandRunRequestDto.ServerIds));
    }

    [Fact]
    public async Task BeginAsync_RejectsCloudInitTemplate()
    {
        var template = new ServerTemplate { Name = "init", Kind = ServerTemplateKind.CloudInit, Content = "#cloud-config" };
        _servers.GetServersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([_web]);
        _templates.GetAsync(template.Id, Arg.Any<CancellationToken>()).Returns(template);

        var result = await _service.BeginAsync(new CommandRunRequestDto { Command = "uptime", ServerIds = [_web.Id], TemplateId = template.Id }, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CommandRunRequestDto.TemplateId));
    }

    [Fact]
    public async Task BeginAsync_CreatesRunWithTargetsAndAudits()
    {
        _servers.GetServersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([_web, _db]);
        CommandRun? saved = null;
        await _repository.AddAsync(Arg.Do<CommandRun>(r => saved = r), Arg.Any<CancellationToken>());

        var result = await _service.BeginAsync(new CommandRunRequestDto
        {
            Command = "uptime\r\ndf -h",
            ServerIds = [_web.Id, _db.Id, _web.Id],
            UseSudo = true,
            TimeoutSeconds = 30
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal("uptime\ndf -h", saved!.Command);
        Assert.Equal(CommandRunStatus.Running, saved.Status);
        Assert.Equal(2, saved.TargetCount);
        Assert.Equal(["db-1", "web-1"], saved.Targets.Select(t => t.ServerName));
        Assert.All(saved.Targets, t => Assert.Equal(CommandTargetStatus.Pending, t.Status));
        Assert.Equal("admin@example.com", saved.UserName);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.CommandRun && e.Details!.Contains("sudo") && e.Details.Contains("df -h")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RecordsResultPerServerAndCompletes()
    {
        var run = Run(_web, _db);
        _repository.GetWithTargetsAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        _executor.ExecuteAsync(_web.Id, run.Command, false, TimeSpan.FromSeconds(30), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(true, 0, false, "ok\n", null));
        _executor.ExecuteAsync(_db.Id, run.Command, false, TimeSpan.FromSeconds(30), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(false, null, false, string.Empty, "Bağlanılamadı."));

        await _service.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        Assert.Equal(CommandRunStatus.Completed, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.Equal(1, run.SucceededCount);
        Assert.Equal(1, run.FailedCount);
        var web = run.Targets.Single(t => t.ServerId == _web.Id);
        Assert.Equal(CommandTargetStatus.Succeeded, web.Status);
        Assert.Equal("ok\n", web.Output);
        var db = run.Targets.Single(t => t.ServerId == _db.Id);
        Assert.Equal(CommandTargetStatus.Failed, db.Status);
        Assert.Equal("Bağlanılamadı.", db.ErrorMessage);
        Assert.Null(db.Output);
    }

    [Fact]
    public async Task ExecuteAsync_MarksTimeoutAndTruncatesOutput()
    {
        var run = Run(_web);
        _repository.GetWithTargetsAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        var longOutput = new string('x', CommandRunRules.MaxOutputChars + 10);
        _executor.ExecuteAsync(_web.Id, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(true, null, true, longOutput, null));

        await _service.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        var target = run.Targets.Single();
        Assert.Equal(CommandTargetStatus.TimedOut, target.Status);
        Assert.True(target.OutputTruncated);
        Assert.Equal(CommandRunRules.MaxOutputChars, target.Output!.Length);
        Assert.Equal(1, run.FailedCount);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsRunThatIsNotRunning()
    {
        var run = Run(_web);
        run.Status = CommandRunStatus.Interrupted;
        _repository.GetWithTargetsAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);

        await _service.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        await _executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default!, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_MarksRemainingTargetsInterruptedOnShutdown()
    {
        var run = Run(_web);
        _repository.GetWithTargetsAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        using var cts = new CancellationTokenSource();
        _executor.ExecuteAsync(_web.Id, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<ScriptExecutionResult>>(async call =>
            {
                await cts.CancelAsync();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return new ScriptExecutionResult(true, 0, false, string.Empty, null);
            });

        await _service.ExecuteAsync(run.Id, cts.Token);

        Assert.Equal(CommandRunStatus.Interrupted, run.Status);
        Assert.Equal(CommandTargetStatus.Interrupted, run.Targets.Single().Status);
    }

    [Fact]
    public async Task InterruptRunningAsync_ClosesOpenRunsWithoutDeleting()
    {
        var run = Run(_web, _db);
        run.Targets.First().Status = CommandTargetStatus.Succeeded;
        _repository.GetRunningAsync(Arg.Any<CancellationToken>()).Returns([run]);

        var count = await _service.InterruptRunningAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(CommandRunStatus.Interrupted, run.Status);
        Assert.Equal(CommandTargetStatus.Succeeded, run.Targets.First().Status);
        Assert.Equal(CommandTargetStatus.Interrupted, run.Targets.Last().Status);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CommandRun Run(params Server[] servers)
    {
        var run = new CommandRun { Command = "uptime", TimeoutSeconds = 30, TargetCount = servers.Length, StartedAt = Now.UtcDateTime };
        foreach (var server in servers)
            run.Targets.Add(new CommandRunTarget { RunId = run.Id, ServerId = server.Id, ServerName = server.Name });
        return run;
    }
}

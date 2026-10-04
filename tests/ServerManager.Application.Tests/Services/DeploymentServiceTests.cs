using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Tests.TestData;
using ServerManager.Application.Validators.Deployments;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class DeploymentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly IDeploymentRepository _repository = Substitute.For<IDeploymentRepository>();
    private readonly IDeploymentDomainService _domains = Substitute.For<IDeploymentDomainService>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IDeploymentProvider _provider = Substitute.For<IDeploymentProvider>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly IDeploymentObserver _observer = Substitute.For<IDeploymentObserver>();
    private readonly IGitIntegrationRegistry _gitIntegrations = Substitute.For<IGitIntegrationRegistry>();
    private readonly RemoteExecutionContext _context = new() { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "deploy" } };
    private readonly DeploymentActor _actor = new("u1", "admin@example.com", "127.0.0.1");
    private readonly Server _server = ProjectTestData.Server();
    private readonly DeploymentProject _project;
    private readonly DeploymentService _service;

    public DeploymentServiceTests()
    {
        _project = ProjectTestData.Project(_server);
        _gitIntegrations.Find(Arg.Any<string>()).Returns((IGitIntegration?)null);
        _gitIntegrations.GetEnabled().Returns([]);
        _repository.GetProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);
        _domains.GetRoutesAsync(Arg.Any<DeploymentProject>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<DeploymentRoute>>.Success([]));
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _server.Id, ServerName = _server.Name, Context = _context }));
        _service = new DeploymentService(
            _repository,
            _domains,
            _connectionProvider,
            _provider,
            _protector,
            _auditLog,
            new StartDeploymentDtoValidator(),
            _gitIntegrations,
            Options.Create(new DeploymentOptions { GitTimeoutSeconds = 300, BuildTimeoutMinutes = 30, DeployTimeoutMinutes = 10, MaxStoredLogKilobytes = 1024 }),
            new FixedTimeProvider(Now),
            NullLogger<DeploymentService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Deployment Started(string? requestedCommit = null)
    {
        var deployment = new Deployment
        {
            ProjectId = _project.Id,
            ProjectName = _project.Name,
            ServerId = _server.Id,
            ServerName = _server.Name,
            Branch = _project.Branch,
            RequestedCommit = requestedCommit,
            Status = DeploymentStatus.Started,
            UserName = _actor.UserName,
            StartedAt = Now.UtcDateTime
        };
        _repository.GetDeploymentAsync(deployment.Id, Arg.Any<CancellationToken>()).Returns(deployment);
        return deployment;
    }

    [Fact]
    public async Task Begin_creates_started_record_and_audits()
    {
        Deployment? added = null;
        await _repository.AddDeploymentAsync(Arg.Do<Deployment>(d => added = d), Arg.Any<CancellationToken>());

        var result = await _service.BeginAsync(_project.Id, new StartDeploymentDto { CommitSha = ProjectTestData.Sha.ToUpperInvariant() }, _actor, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(DeploymentStatus.Started, added.Status);
        Assert.Equal(ProjectTestData.Sha, added.RequestedCommit);
        Assert.Equal("web-01", added.ServerName);
        Assert.Equal("admin@example.com", added.UserName);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DeploymentStart && e.UserNameOverride == "admin@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Begin_is_rejected_while_another_deployment_runs()
    {
        _repository.GetRunningDeploymentAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(new Deployment { ProjectId = _project.Id });

        var result = await _service.BeginAsync(_project.Id, new StartDeploymentDto(), _actor, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _repository.DidNotReceiveWithAnyArgs().AddDeploymentAsync(default!, Ct);
    }

    [Fact]
    public async Task Redeploy_requires_commit_of_source()
    {
        var source = Started();

        var result = await _service.BeginRedeployAsync(source.Id, _actor, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("commit bilgisi yok", result.Message);
    }

    [Fact]
    public async Task Redeploy_uses_source_commit()
    {
        var source = Started();
        source.Status = DeploymentStatus.Succeeded;
        source.CommitSha = ProjectTestData.Sha;
        Deployment? added = null;
        await _repository.AddDeploymentAsync(Arg.Do<Deployment>(d => added = d), Arg.Any<CancellationToken>());

        var result = await _service.BeginRedeployAsync(source.Id, _actor, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectTestData.Sha, added!.RequestedCommit);
        Assert.Equal(source.Id, added.SourceDeploymentId);
    }

    [Fact]
    public async Task Run_success_records_commit_status_log_and_stage_timestamps()
    {
        var deployment = Started();
        _project.EncryptedAccessToken = _protector.Protect("ghp_secret");
        _project.EncryptedEnvironment = _protector.Protect("A=1\n");
        _provider.DeployAsync(_context, Arg.Any<DeploymentPlan>(), Arg.Any<IDeploymentObserver>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var observer = call.Arg<IDeploymentObserver>();
                await observer.OnStageAsync(DeploymentStage.Building, "Build", CancellationToken.None);
                await observer.OnOutputAsync("building...\r\n", CancellationToken.None);
                await observer.OnStageAsync(DeploymentStage.Deploying, "Deploy", CancellationToken.None);
                return ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult
                {
                    Succeeded = true,
                    CommitSha = ProjectTestData.Sha,
                    CommitAuthor = "Ayşe",
                    CommitMessage = "Fix"
                });
            });
        using var cancellation = new DeploymentCancellation(CancellationToken.None);

        var result = await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.True(result.IsSuccess);
        Assert.Equal(DeploymentStatus.Succeeded, deployment.Status);
        Assert.Equal(ProjectTestData.Sha, deployment.CommitSha);
        Assert.Equal("Ayşe", deployment.CommitAuthor);
        Assert.NotNull(deployment.BuildStartedAt);
        Assert.NotNull(deployment.DeployStartedAt);
        Assert.NotNull(deployment.CompletedAt);
        Assert.Contains("building...", deployment.Log);
        Assert.DoesNotContain("ghp_secret", deployment.Log);
        await _provider.Received(1).DeployAsync(
            _context,
            Arg.Is<DeploymentPlan>(p => p.Source.AccessToken == "ghp_secret" && p.Environment == "A=1\n" && p.Slug == "musteri-api" && p.Commit == null),
            Arg.Any<IDeploymentObserver>(),
            Arg.Any<CancellationToken>());
        await _observer.Received(1).OnStageAsync(DeploymentStage.Completed, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DeploymentComplete && e.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_with_integration_uses_short_lived_token_instead_of_stored_one()
    {
        var deployment = Started();
        _project.EncryptedAccessToken = _protector.Protect("ghp_old");
        _project.GitIntegration = "Git.GitHub";
        _project.GitSourceId = "app:42";
        _project.GitRepository = "acme/api";
        var integration = Substitute.For<IGitIntegration>();
        integration.DisplayName.Returns("GitHub App");
        integration.CreateAccessTokenAsync("app:42", "acme/api", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitAccessToken>.Success(new GitAccessToken("x-access-token", "ghs_short")));
        _gitIntegrations.Find("Git.GitHub").Returns(integration);
        _provider.DeployAsync(_context, Arg.Any<DeploymentPlan>(), Arg.Any<IDeploymentObserver>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult { Succeeded = true, CommitSha = ProjectTestData.Sha }));
        using var cancellation = new DeploymentCancellation(CancellationToken.None);

        var result = await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("ghs_short", deployment.Log);
        await _provider.Received(1).DeployAsync(
            _context,
            Arg.Is<DeploymentPlan>(p => p.Source.AccessToken == "ghs_short" && p.Source.Username == "x-access-token"),
            Arg.Any<IDeploymentObserver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_fails_when_project_integration_is_disabled()
    {
        var deployment = Started();
        _project.GitIntegration = "Git.GitHub";
        _project.GitSourceId = "app:42";
        _project.GitRepository = "acme/api";
        using var cancellation = new DeploymentCancellation(CancellationToken.None);

        await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Contains("Git.GitHub", deployment.FailureReason);
        await _provider.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!, default!, Ct);
    }

    [Fact]
    public async Task Run_failure_records_reason()
    {
        var deployment = Started();
        _provider.DeployAsync(_context, Arg.Any<DeploymentPlan>(), Arg.Any<IDeploymentObserver>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult { Succeeded = false, FailureReason = "Build başarısız: x", ExitCode = 1 }));
        using var cancellation = new DeploymentCancellation(CancellationToken.None);

        var result = await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.False(result.IsSuccess);
        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Equal("Build başarısız: x", deployment.FailureReason);
        Assert.Equal(1, deployment.ExitCode);
        await _observer.Received(1).OnStageAsync(DeploymentStage.Failed, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_fails_without_calling_provider_when_secrets_cannot_be_decrypted()
    {
        var deployment = Started();
        _project.EncryptedAccessToken = "corrupted";
        using var cancellation = new DeploymentCancellation(CancellationToken.None);

        await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Contains("çözülemedi", deployment.FailureReason);
        await _provider.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!, default!, Ct);
    }

    [Fact]
    public async Task User_cancel_is_recorded_as_cancelled()
    {
        var deployment = Started();
        using var cancellation = new DeploymentCancellation(CancellationToken.None);
        var canceller = new DeploymentActor("u2", "operator", "10.0.0.1");
        _provider.DeployAsync(_context, Arg.Any<DeploymentPlan>(), Arg.Any<IDeploymentObserver>(), Arg.Any<CancellationToken>())
            .Returns<Task<ServiceResult<DeploymentRunResult>>>(async call =>
            {
                await call.Arg<IDeploymentObserver>().OnCommitAsync(new DeploymentCommit(ProjectTestData.Sha, "Ayşe", "Yavaş build"), CancellationToken.None);
                cancellation.Cancel(canceller);
                throw new OperationCanceledException(call.Arg<CancellationToken>());
            });

        var result = await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.False(result.IsSuccess);
        Assert.Equal(DeploymentStatus.Cancelled, deployment.Status);
        Assert.Equal(ProjectTestData.Sha, deployment.CommitSha);
        Assert.Equal("Yavaş build", deployment.CommitMessage);
        await _observer.Received(1).OnCommitAsync(Arg.Is<DeploymentCommit>(c => c.Sha == ProjectTestData.Sha), Arg.Any<CancellationToken>());
        Assert.Equal("operator", deployment.CancelledBy);
        Assert.Equal(DeploymentService.CancelledReason, deployment.FailureReason);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.DeploymentCancel && e.UserNameOverride == "operator"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Shutdown_is_recorded_as_interrupted()
    {
        var deployment = Started();
        using var shutdown = new CancellationTokenSource();
        using var cancellation = new DeploymentCancellation(shutdown.Token);
        _provider.DeployAsync(_context, Arg.Any<DeploymentPlan>(), Arg.Any<IDeploymentObserver>(), Arg.Any<CancellationToken>())
            .Returns<Task<ServiceResult<DeploymentRunResult>>>(call =>
            {
                shutdown.Cancel();
                throw new OperationCanceledException(call.Arg<CancellationToken>());
            });

        await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.Equal(DeploymentStatus.Interrupted, deployment.Status);
        Assert.Null(deployment.CancelledBy);
        Assert.Equal(DeploymentService.InterruptedReason, deployment.FailureReason);
    }

    [Fact]
    public async Task Run_ignores_records_that_are_not_waiting_to_start()
    {
        var deployment = Started();
        deployment.Status = DeploymentStatus.Succeeded;
        using var cancellation = new DeploymentCancellation(CancellationToken.None);

        var result = await _service.RunAsync(deployment.Id, _actor, _observer, cancellation);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        await _provider.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!, default!, Ct);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces;
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

public class ProjectServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly IDeploymentRepository _repository = Substitute.For<IDeploymentRepository>();
    private readonly IServerRepository _serverRepository = Substitute.For<IServerRepository>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IDeploymentProvider _provider = Substitute.For<IDeploymentProvider>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly Server _server = ProjectTestData.Server();
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _serverRepository.GetByIdAsync(_server.Id, Arg.Any<CancellationToken>()).Returns(_server);
        _service = new ProjectService(
            _repository,
            _serverRepository,
            _connectionProvider,
            _provider,
            _protector,
            _auditLog,
            _currentUser,
            new CreateProjectDtoValidator(),
            new UpdateProjectDtoValidator(),
            Options.Create(new DeploymentOptions { GitTimeoutSeconds = 300 }),
            new FixedTimeProvider(Now),
            NullLogger<ProjectService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_encrypts_secrets_generates_unique_slug_and_audits()
    {
        DeploymentProject? added = null;
        await _repository.AddProjectAsync(Arg.Do<DeploymentProject>(p => added = p), Arg.Any<CancellationToken>());
        _repository.SlugExistsAsync("musteri-api", Arg.Any<CancellationToken>()).Returns(true);
        var dto = ProjectTestData.ValidCreateDto(_server.Id);
        dto.Name = "  Müşteri API ";
        dto.AccessToken = "ghp_secret";
        dto.Environment = "A=1\r\nB=2";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("Müşteri API", added.Name);
        Assert.Equal("musteri-api-2", added.Slug);
        Assert.Equal("ghp_secret", _protector.Unprotect(added.EncryptedAccessToken!));
        Assert.Equal("A=1\nB=2\n", _protector.Unprotect(added.EncryptedEnvironment!));
        Assert.Equal("admin@example.com", added.CreatedBy);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectCreate && e.Details != null && !e.Details.Contains("ghp_secret")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_duplicate_name()
    {
        _repository.ProjectNameExistsAsync("Müşteri API", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.CreateAsync(ProjectTestData.ValidCreateDto(_server.Id), Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProjectDto.Name));
        await _repository.DidNotReceiveWithAnyArgs().AddProjectAsync(default!, Ct);
    }

    [Fact]
    public async Task Update_keeps_stored_token_when_left_blank()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("old-token");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var dto = ProjectTestData.ValidUpdateDto(project);
        dto.Branch = "develop";

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("develop", project.Branch);
        Assert.Equal("old-token", _protector.Unprotect(project.EncryptedAccessToken!));
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectUpdate && e.Details!.Contains("Dal: develop")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_rejects_stored_token_with_non_https_url()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("old-token");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var dto = ProjectTestData.ValidUpdateDto(project);
        dto.RepositoryUrl = "git@github.com:acme/api.git";

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProjectDto.RepositoryUrl));
        Assert.Equal("https://github.com/acme/api.git", project.RepositoryUrl);
    }

    [Fact]
    public async Task Update_can_remove_token_and_environment()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("old-token");
        project.EncryptedEnvironment = _protector.Protect("A=1\n");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var dto = ProjectTestData.ValidUpdateDto(project);
        dto.RepositoryUrl = "git@github.com:acme/api.git";
        dto.RemoveAccessToken = true;
        dto.RemoveEnvironment = true;

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(project.EncryptedAccessToken);
        Assert.Null(project.EncryptedEnvironment);
    }

    [Fact]
    public async Task Update_is_blocked_while_deploying()
    {
        var project = ProjectTestData.Project(_server);
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        _repository.GetRunningDeploymentAsync(project.Id, Arg.Any<CancellationToken>()).Returns(new Deployment { ProjectId = project.Id });

        var result = await _service.UpdateAsync(ProjectTestData.ValidUpdateDto(project), Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Delete_requires_exact_name_and_soft_deletes()
    {
        var project = ProjectTestData.Project(_server);
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var wrong = await _service.DeleteAsync(project.Id, "müşteri api", Ct);
        Assert.Equal(ServiceErrorType.Validation, wrong.ErrorType);
        Assert.False(project.IsDeleted);

        var result = await _service.DeleteAsync(project.Id, " Müşteri API ", Ct);

        Assert.True(result.IsSuccess);
        Assert.True(project.IsDeleted);
        Assert.Equal(Now.UtcDateTime, project.DeletedAt);
        Assert.Equal("admin@example.com", project.DeletedBy);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectDelete), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_branches_passes_decrypted_token_and_reports_configured_branch()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("ghp_secret");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var context = new RemoteExecutionContext { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "deploy" } };
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _server.Id, ServerName = _server.Name, Context = context }));
        _provider.ListBranchesAsync(context, Arg.Is<GitSource>(s => s.AccessToken == "ghp_secret" && s.Username == "x-access-token"), TimeSpan.FromSeconds(120), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<string>>.Success(["develop", "main"]));

        var result = await _service.ListBranchesAsync(project.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.ConfiguredBranchExists);
        Assert.Equal(["develop", "main"], result.Data.Branches);
    }

    [Fact]
    public async Task Details_report_unreadable_environment()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedEnvironment = "corrupted";
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var result = await _service.GetDetailsAsync(project.Id, Ct);

        Assert.True(result.Data!.EnvironmentUnreadable);
        Assert.Empty(result.Data.EnvironmentKeys);
    }
}

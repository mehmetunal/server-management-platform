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
    private readonly IDeploymentDomainService _domains = Substitute.For<IDeploymentDomainService>();
    private readonly IServerRepository _serverRepository = Substitute.For<IServerRepository>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IDeploymentProvider _provider = Substitute.For<IDeploymentProvider>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGitIntegrationRegistry _gitIntegrations = Substitute.For<IGitIntegrationRegistry>();
    private readonly Server _server = ProjectTestData.Server();
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _gitIntegrations.Find(Arg.Any<string>()).Returns((IGitIntegration?)null);
        _gitIntegrations.GetEnabled().Returns([]);
        _serverRepository.GetByIdAsync(_server.Id, Arg.Any<CancellationToken>()).Returns(_server);
        _service = new ProjectService(
            _repository,
            _domains,
            _serverRepository,
            _connectionProvider,
            _provider,
            _protector,
            _auditLog,
            _currentUser,
            new CreateProjectDtoValidator(),
            new UpdateProjectDtoValidator(),
            new RemoteBranchQueryDtoValidator(),
            _gitIntegrations,
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

        var wrong = await _service.DeleteAsync(project.Id, "müşteri api", false, Ct);
        Assert.Equal(ServiceErrorType.Validation, wrong.ErrorType);
        Assert.False(project.IsDeleted);

        var result = await _service.DeleteAsync(project.Id, " Müşteri API ", false, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(project.IsDeleted);
        Assert.Equal(Now.UtcDateTime, project.DeletedAt);
        Assert.Equal("admin@example.com", project.DeletedBy);
        await _domains.Received(1).OnProjectDeletedAsync(project, Arg.Any<CancellationToken>(), true);
        await _provider.DidNotReceiveWithAnyArgs().RemoveDeploymentAsync(null!, null!, default, Ct);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectDelete), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hard_delete_removes_the_server_copy_and_skips_a_second_routing_update()
    {
        var project = ProjectTestData.Project(_server);
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var context = new RemoteExecutionContext { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "deploy" } };
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _server.Id, ServerName = _server.Name, Context = context }));
        _provider.RemoveDeploymentAsync(context, Arg.Is<DeploymentPlan>(p => p.DeployPath == project.DeployPath && p.Slug == project.Slug), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success("Sunucudaki uygulama silindi."));

        var result = await _service.DeleteAsync(project.Id, project.Name, true, Ct);

        Assert.True(result.IsSuccess);
        Assert.Contains("kaldırıldı", result.Message, StringComparison.Ordinal);
        Assert.True(project.IsDeleted);
        await _domains.Received(1).OnProjectDeletedAsync(project, Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task Hard_delete_keeps_the_project_when_the_server_cleanup_fails()
    {
        var project = ProjectTestData.Project(_server);
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var context = new RemoteExecutionContext { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "deploy" } };
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _server.Id, ServerName = _server.Name, Context = context }));
        _provider.RemoveDeploymentAsync(context, Arg.Any<DeploymentPlan>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Failure("Sunucudaki uygulama silinemedi."));

        var result = await _service.DeleteAsync(project.Id, project.Name, true, Ct);

        Assert.False(result.IsSuccess);
        Assert.False(project.IsDeleted);
        await _domains.DidNotReceiveWithAnyArgs().OnProjectDeletedAsync(null!, default, false);
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

    private IGitIntegration EnableIntegration()
    {
        var integration = Substitute.For<IGitIntegration>();
        integration.SystemName.Returns("Git.GitHub");
        integration.DisplayName.Returns("GitHub App");
        integration.Provider.Returns(GitProvider.GitHub);
        _gitIntegrations.Find("Git.GitHub").Returns(integration);
        _gitIntegrations.GetEnabled().Returns([integration]);
        return integration;
    }

    [Fact]
    public async Task Create_with_integration_takes_clone_url_from_integration_and_drops_manual_credentials()
    {
        var integration = EnableIntegration();
        integration.GetRepositoryAsync("app:42", "acme/api", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitRepositoryDto>.Success(new GitRepositoryDto("Acme/Api", "https://github.com/Acme/Api.git", "main", true, null)));
        DeploymentProject? added = null;
        await _repository.AddProjectAsync(Arg.Do<DeploymentProject>(p => added = p), Arg.Any<CancellationToken>());
        var dto = ProjectTestData.ValidCreateDto(_server.Id);
        dto.GitSource = "Git.GitHub|app:42";
        dto.GitRepository = "acme/api";
        dto.RepositoryUrl = "https://evil.example.com/x.git";
        dto.AccessToken = "ghp_manual";
        dto.GitUsername = "someone";
        dto.GitProvider = GitProvider.SelfHosted;

        var result = await _service.CreateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("Git.GitHub", added.GitIntegration);
        Assert.Equal("app:42", added.GitSourceId);
        Assert.Equal("Acme/Api", added.GitRepository);
        Assert.Equal("https://github.com/Acme/Api.git", added.RepositoryUrl);
        Assert.Equal(GitProvider.GitHub, added.GitProvider);
        Assert.Null(added.EncryptedAccessToken);
        Assert.Null(added.GitUsername);
    }

    [Fact]
    public async Task Create_with_unknown_repository_returns_field_error()
    {
        var integration = EnableIntegration();
        integration.GetRepositoryAsync("app:42", "acme/other", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitRepositoryDto>.NotFound("Depo bu kurulumda yok."));
        var dto = ProjectTestData.ValidCreateDto(_server.Id);
        dto.GitSource = "Git.GitHub|app:42";
        dto.GitRepository = "acme/other";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ProjectFormDto.GitRepository) && e.Message == "Depo bu kurulumda yok.");
        await _repository.DidNotReceiveWithAnyArgs().AddProjectAsync(default!, Ct);
    }

    [Fact]
    public async Task Create_with_disabled_integration_is_rejected()
    {
        var dto = ProjectTestData.ValidCreateDto(_server.Id);
        dto.GitSource = "Git.GitHub|app:42";
        dto.GitRepository = "acme/api";

        var result = await _service.CreateAsync(dto, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ProjectFormDto.GitSource));
    }

    [Fact]
    public async Task Integration_exception_is_reported_without_crashing()
    {
        var integration = EnableIntegration();
        integration.ListSourcesAsync(Arg.Any<CancellationToken>()).Returns<Task<ServiceResult<IReadOnlyList<GitSourceDto>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.GetGitSourcesAsync(Ct);

        Assert.Empty(result.Sources);
        Assert.Single(result.Warnings);
        Assert.DoesNotContain("boom", result.Warnings[0]);
    }

    [Fact]
    public async Task Git_sources_are_prefixed_with_integration_name()
    {
        var integration = EnableIntegration();
        integration.ListSourcesAsync(Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<GitSourceDto>>.Success([new GitSourceDto("app:42", "acme · Server Manager")]));

        var result = await _service.GetGitSourcesAsync(Ct);

        var source = Assert.Single(result.Sources);
        Assert.Equal("Git.GitHub|app:42", source.Key);
        Assert.Equal("GitHub App", source.IntegrationName);
    }

    [Fact]
    public async Task Update_switching_to_integration_removes_stored_token()
    {
        var integration = EnableIntegration();
        integration.GetRepositoryAsync("app:42", "acme/api", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitRepositoryDto>.Success(new GitRepositoryDto("acme/api", "https://github.com/acme/api.git", "main", true, null)));
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("old-token");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var dto = ProjectTestData.ValidUpdateDto(project);
        dto.GitSource = "Git.GitHub|app:42";
        dto.GitRepository = "acme/api";

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(project.EncryptedAccessToken);
        Assert.Equal("Git.GitHub", project.GitIntegration);
    }

    [Fact]
    public async Task Update_rejects_stored_token_when_repository_host_changes()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("old-token");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var dto = ProjectTestData.ValidUpdateDto(project);
        dto.RepositoryUrl = "https://attacker.example.com/acme/api.git";

        var result = await _service.UpdateAsync(dto, Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProjectDto.AccessToken));
        Assert.Equal("https://github.com/acme/api.git", project.RepositoryUrl);
    }

    [Fact]
    public async Task Remote_branches_use_stored_token_only_for_same_host()
    {
        var project = ProjectTestData.Project(_server);
        project.EncryptedAccessToken = _protector.Protect("ghp_secret");
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        var context = new RemoteExecutionContext { Connection = new SshConnectionRequest { Host = "203.0.113.10", Username = "deploy" } };
        _connectionProvider.GetAsync(_server.Id, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<ServerConnection>.Success(new ServerConnection { ServerId = _server.Id, ServerName = _server.Name, Context = context }));
        _provider.ListBranchesAsync(context, Arg.Any<GitSource>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<string>>.Success(["main"]));

        await _service.ListRemoteBranchesAsync(new RemoteBranchQueryDto
        {
            ServerId = _server.Id,
            ProjectId = project.Id,
            GitProvider = GitProvider.GitHub,
            RepositoryUrl = "https://github.com/acme/other.git"
        }, Ct);
        await _service.ListRemoteBranchesAsync(new RemoteBranchQueryDto
        {
            ServerId = _server.Id,
            ProjectId = project.Id,
            GitProvider = GitProvider.SelfHosted,
            RepositoryUrl = "https://attacker.example.com/acme/api.git"
        }, Ct);

        await _provider.Received(1).ListBranchesAsync(context,
            Arg.Is<GitSource>(s => s.RepositoryUrl == "https://github.com/acme/other.git" && s.AccessToken == "ghp_secret"),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _provider.Received(1).ListBranchesAsync(context,
            Arg.Is<GitSource>(s => s.RepositoryUrl == "https://attacker.example.com/acme/api.git" && s.AccessToken == null),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_branches_of_integration_project_asks_the_integration()
    {
        var integration = EnableIntegration();
        integration.ListBranchesAsync("app:42", "acme/api", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<string>>.Success(["main", "release"]));
        var project = ProjectTestData.Project(_server);
        project.GitIntegration = "Git.GitHub";
        project.GitSourceId = "app:42";
        project.GitRepository = "acme/api";
        _repository.GetProjectAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var result = await _service.ListBranchesAsync(project.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["main", "release"], result.Data!.Branches);
        await _provider.DidNotReceiveWithAnyArgs().ListBranchesAsync(default!, default!, default, Ct);
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

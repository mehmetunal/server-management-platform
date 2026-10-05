using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
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

public class DeploymentDomainServiceTests
{
    private readonly IDeploymentRepository _projects = Substitute.For<IDeploymentRepository>();
    private readonly IDeploymentDomainRepository _domains = Substitute.For<IDeploymentDomainRepository>();
    private readonly IServerConnectionProvider _connectionProvider = Substitute.For<IServerConnectionProvider>();
    private readonly IDeploymentProvider _provider = Substitute.For<IDeploymentProvider>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly FakeSecretProtector _protector = new();
    private readonly DeploymentProject _project = ProjectTestData.Project(ProjectTestData.Server());
    private readonly DeploymentDomainService _service;

    public DeploymentDomainServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _projects.GetProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);
        _domains.ListByProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _service = new DeploymentDomainService(
            _projects,
            _domains,
            _connectionProvider,
            _provider,
            _protector,
            Substitute.For<IAuditLogService>(),
            _currentUser,
            new DomainFormDtoValidator(),
            Options.Create(new DeploymentOptions()),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<DeploymentDomainService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DeploymentDomain Domain(string host = "api.ornek.com", string? service = "web", DeploymentTlsMode mode = DeploymentTlsMode.LetsEncrypt) => new()
    {
        ProjectId = _project.Id,
        ServerId = _project.ServerId,
        Host = host,
        ContainerPort = 8080,
        ServiceName = service,
        TlsMode = mode
    };

    private DomainFormDto Form(Guid? id = null, DeploymentTlsMode mode = DeploymentTlsMode.Cloudflare) => new()
    {
        Id = id,
        Host = "api.ornek.com",
        ContainerPort = 8080,
        ServiceName = "web",
        TlsMode = mode
    };

    [Fact]
    public async Task Domain_changes_are_refused_while_a_deployment_runs()
    {
        var domain = Domain();
        _domains.GetAsync(domain.Id, Arg.Any<CancellationToken>()).Returns(domain);
        _projects.GetRunningDeploymentAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(new Deployment { ProjectId = _project.Id });

        var save = await _service.SaveAsync(_project.Id, Form(), Ct);
        var delete = await _service.DeleteAsync(_project.Id, domain.Id, domain.Host, Ct);
        var apply = await _service.ApplyAsync(_project.Id, Ct);

        Assert.All(new[] { save, delete, apply }, r => Assert.Equal(ServiceErrorType.Conflict, r.ErrorType));
        Assert.False(domain.IsDeleted);
        await _domains.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
        await _provider.DidNotReceiveWithAnyArgs().ApplyRoutingAsync(default!, default!, default, Ct);
    }

    [Fact]
    public async Task Switching_to_custom_certificate_without_a_stored_one_requires_the_certificate()
    {
        var domain = Domain();
        _domains.GetAsync(domain.Id, Arg.Any<CancellationToken>()).Returns(domain);

        var result = await _service.SaveAsync(_project.Id, Form(domain.Id, DeploymentTlsMode.Custom), Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DomainFormDto.CertificatePem));
        Assert.Equal(DeploymentTlsMode.LetsEncrypt, domain.TlsMode);
        await _domains.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Compose_routes_without_a_service_name_are_not_built()
    {
        _domains.ListByProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns([Domain(service: null)]);

        var routes = await _service.GetRoutesAsync(_project, Ct);

        Assert.False(routes.IsSuccess);
        Assert.Contains("servis adı", routes.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Switching_to_compose_requires_service_names_on_every_domain()
    {
        _project.BuildType = DeploymentBuildType.Dockerfile;
        _domains.ListByProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns([Domain(service: null)]);

        var compose = await _service.ValidateProjectChangeAsync(_project, _project.ServerId, DeploymentBuildType.DockerCompose, Ct);
        var dockerfile = await _service.ValidateProjectChangeAsync(_project, _project.ServerId, DeploymentBuildType.Dockerfile, Ct);

        Assert.Equal(ServiceErrorType.Validation, compose.ErrorType);
        Assert.True(dockerfile.IsSuccess);
    }

    [Fact]
    public async Task Moving_to_a_server_where_the_host_is_taken_is_refused()
    {
        var domain = Domain();
        var target = Guid.NewGuid();
        _domains.ListByProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns([domain]);
        _domains.HostPathExistsAsync(target, domain.Host, domain.Path, domain.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.ValidateProjectChangeAsync(_project, target, _project.BuildType, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Moved_project_takes_its_domains_to_the_new_server()
    {
        var domain = Domain();
        _domains.ListByProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns([domain]);
        _project.ServerId = Guid.NewGuid();

        await _service.OnProjectServerChangedAsync(_project, Ct);

        Assert.Equal(_project.ServerId, domain.ServerId);
        Assert.Equal("admin@example.com", domain.UpdatedBy);
        await _domains.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

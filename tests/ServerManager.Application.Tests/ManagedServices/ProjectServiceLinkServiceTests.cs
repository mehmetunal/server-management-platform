using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Tests.TestData;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

public class ProjectServiceLinkServiceTests
{
    private const string Password = "S3cret-Pa55word";

    private readonly IProjectServiceLinkRepository _links = Substitute.For<IProjectServiceLinkRepository>();
    private readonly IDeploymentRepository _projects = Substitute.For<IDeploymentRepository>();
    private readonly IManagedServiceService _services = Substitute.For<IManagedServiceService>();
    private readonly IProjectEnvironmentService _environment = Substitute.For<IProjectEnvironmentService>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly DeploymentProject _project;
    private readonly ManagedServiceConnectionInfo _info;
    private readonly ProjectServiceLinkService _service;

    public ProjectServiceLinkServiceTests()
    {
        var server = ProjectTestData.Server();
        _project = ProjectTestData.Project(server);
        _info = new ManagedServiceConnectionInfo
        {
            ServiceId = Guid.NewGuid(),
            ServerId = server.Id,
            Name = "db",
            TemplateKey = "postgres",
            Host = "sm-svc-db",
            Port = 5432,
            Username = "app",
            Password = Password,
            SuggestedEnvironment = new Dictionary<string, string>
            {
                ["DATABASE_URL"] = $"postgres://app:{Password}@sm-svc-db:5432/app",
                ["PGHOST"] = "sm-svc-db"
            },
            SuggestedEnvironmentPreview = new Dictionary<string, string>
            {
                ["DATABASE_URL"] = "postgres://app:****@sm-svc-db:5432/app",
                ["PGHOST"] = "sm-svc-db"
            }
        };
        _services.GetConnectionInfoAsync(_info.ServiceId, Arg.Any<CancellationToken>()).Returns(ServiceResult<ManagedServiceConnectionInfo>.Success(_info));
        _projects.GetProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);
        _currentUser.UserName.Returns("admin@example.com");
        _environment.UpsertEnvironmentVariablesAsync(_project.Id, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => ServiceResult<EnvironmentChangeResultDto>.Success(new EnvironmentChangeResultDto
            {
                Added = call.ArgAt<IReadOnlyDictionary<string, string>>(1).Keys.ToList()
            }));
        _service = new ProjectServiceLinkService(
            _links,
            _projects,
            _services,
            _environment,
            _auditLog,
            _currentUser,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ProjectServiceLinkService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LinkServiceToProjectDto Form(params ServiceLinkVariableDto[] variables) => new() { ProjectId = _project.Id, Variables = [.. variables] };

    [Fact]
    public async Task Preview_lists_same_server_projects_with_masked_values()
    {
        var commands = ProjectTestData.Project(_project.Server!);
        commands.BuildType = DeploymentBuildType.Commands;
        _projects.GetProjectsByServerAsync(_info.ServerId, Arg.Any<CancellationToken>()).Returns([_project, commands]);
        _environment.GetEnvironmentKeysAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(ServiceResult<IReadOnlyList<string>>.Success(["PGHOST"]));
        _links.ListByServiceAsync(_info.ServiceId, Arg.Any<CancellationToken>()).Returns([]);

        var preview = await _service.GetPreviewAsync(_info.ServiceId, Ct);

        var project = Assert.Single(preview.Data!.Projects);
        Assert.Equal(_project.Id, project.Id);
        Assert.Equal(["PGHOST"], project.ExistingKeys);
        Assert.DoesNotContain(preview.Data.Variables, v => v.Preview.Contains(Password, StringComparison.Ordinal));
        Assert.Contains(preview.Data.Variables, v => v.Key == "DATABASE_URL" && v.Preview.Contains("****", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Link_writes_selected_variables_with_renamed_keys_and_records_the_link()
    {
        ProjectServiceLink? saved = null;
        await _links.AddAsync(Arg.Do<ProjectServiceLink>(l => saved = l), Arg.Any<CancellationToken>());

        var result = await _service.LinkAsync(_info.ServiceId, Form(
            new ServiceLinkVariableDto { SourceKey = "DATABASE_URL", Key = "DB_URL", Include = true },
            new ServiceLinkVariableDto { SourceKey = "PGHOST", Key = "PGHOST", Include = false }), Ct);

        Assert.True(result.IsSuccess, result.Message);
        await _environment.Received(1).UpsertEnvironmentVariablesAsync(
            _project.Id,
            Arg.Is<IReadOnlyDictionary<string, string>>(d => d.Count == 1 && d["DB_URL"] == _info.SuggestedEnvironment["DATABASE_URL"]),
            false,
            Arg.Any<CancellationToken>());
        Assert.NotNull(saved);
        Assert.Equal("DB_URL", saved!.EnvironmentKeys);
        Assert.Equal(_info.ServiceId, saved.ManagedServiceId);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectServiceLink && !e.Details!.Contains(Password)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Link_rejects_project_on_another_server()
    {
        _project.ServerId = Guid.NewGuid();

        var result = await _service.LinkAsync(_info.ServiceId, Form(), Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        await _links.DidNotReceive().AddAsync(Arg.Any<ProjectServiceLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Link_rejects_existing_link_invalid_keys_and_unknown_variables()
    {
        var invalid = await _service.LinkAsync(_info.ServiceId, Form(new ServiceLinkVariableDto { SourceKey = "PGHOST", Key = "1-bad", Include = true }), Ct);
        var unknown = await _service.LinkAsync(_info.ServiceId, Form(new ServiceLinkVariableDto { SourceKey = "SECRET", Include = true }), Ct);
        _links.FindAnyAsync(_project.Id, _info.ServiceId, Arg.Any<CancellationToken>()).Returns(new ProjectServiceLink());
        var duplicate = await _service.LinkAsync(_info.ServiceId, Form(), Ct);

        Assert.Equal(ServiceErrorType.Validation, invalid.ErrorType);
        Assert.Equal(ServiceErrorType.Validation, unknown.ErrorType);
        Assert.Equal(ServiceErrorType.Validation, duplicate.ErrorType);
        await _environment.DidNotReceiveWithAnyArgs().UpsertEnvironmentVariablesAsync(default, default!, default, Ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_removes_added_keys_only_when_requested(bool removeKeys)
    {
        var link = new ProjectServiceLink
        {
            ProjectId = _project.Id,
            Project = _project,
            ManagedServiceId = _info.ServiceId,
            ManagedService = new ManagedService { Name = "db", ContainerName = "sm-svc-db" },
            EnvironmentKeys = "DB_URL,PGHOST"
        };
        _links.GetAsync(link.Id, Arg.Any<CancellationToken>()).Returns(link);
        _environment.RemoveEnvironmentVariablesAsync(_project.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<EnvironmentChangeResultDto>.Success(new EnvironmentChangeResultDto { Removed = ["DB_URL", "PGHOST"] }));

        var result = await _service.UnlinkAsync(link.Id, removeKeys, Ct);

        Assert.True(result.IsSuccess);
        _links.Received(1).Remove(link);
        await _environment.Received(removeKeys ? 1 : 0).RemoveEnvironmentVariablesAsync(
            _project.Id, Arg.Is<IReadOnlyCollection<string>>(k => k.SequenceEqual(new[] { "DB_URL", "PGHOST" })), Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectServiceUnlink), Arg.Any<CancellationToken>());
    }
}

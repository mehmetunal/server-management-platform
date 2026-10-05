using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Application.Tests.TestData;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Tests.Services;

public class ProjectEnvironmentServiceTests
{
    private readonly IDeploymentRepository _repository = Substitute.For<IDeploymentRepository>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly FakeSecretProtector _protector = new();
    private readonly DeploymentProject _project;
    private readonly ProjectEnvironmentService _service;

    public ProjectEnvironmentServiceTests()
    {
        _project = ProjectTestData.Project(ProjectTestData.Server());
        _project.EncryptedEnvironment = _protector.Protect("# db\nDATABASE_URL=postgres://db\nAPI_KEY=old\n");
        _repository.GetProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);
        _currentUser.UserName.Returns("admin@example.com");
        _service = new ProjectEnvironmentService(
            _repository,
            _protector,
            _auditLog,
            _currentUser,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ProjectEnvironmentService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Stored() => _protector.Unprotect(_project.EncryptedEnvironment!);

    [Fact]
    public async Task Upsert_adds_new_keys_and_keeps_existing_ones_without_overwrite()
    {
        var result = await _service.UpsertEnvironmentVariablesAsync(
            _project.Id,
            new Dictionary<string, string> { ["API_KEY"] = "new", ["REDIS_URL"] = "redis://cache:6379" },
            overwrite: false,
            Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["REDIS_URL"], result.Data!.Added);
        Assert.Equal(["API_KEY"], result.Data.Skipped);
        Assert.Equal("# db\nDATABASE_URL=postgres://db\nAPI_KEY=old\nREDIS_URL=redis://cache:6379\n", Stored());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectEnvironmentUpdate && e.Details!.Contains("REDIS_URL") && !e.Details.Contains("redis://")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_deletes_only_existing_keys_and_keeps_comments()
    {
        var result = await _service.RemoveEnvironmentVariablesAsync(_project.Id, ["API_KEY", "MISSING"], Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["API_KEY"], result.Data!.Removed);
        Assert.Equal("# db\nDATABASE_URL=postgres://db\n", Stored());
    }

    [Fact]
    public async Task Upsert_with_overwrite_updates_values_in_place()
    {
        var result = await _service.UpsertEnvironmentVariablesAsync(_project.Id, new Dictionary<string, string> { ["API_KEY"] = "new" }, overwrite: true, Ct);

        Assert.Equal(["API_KEY"], result.Data!.Updated);
        Assert.Equal("# db\nDATABASE_URL=postgres://db\nAPI_KEY=new\n", Stored());
    }

    [Fact]
    public async Task Upsert_rejects_invalid_input_without_writing()
    {
        var badKey = await _service.UpsertEnvironmentVariablesAsync(_project.Id, new Dictionary<string, string> { ["1BAD"] = "x" }, true, Ct);
        var badValue = await _service.UpsertEnvironmentVariablesAsync(_project.Id, new Dictionary<string, string> { ["OK"] = "a\nb" }, true, Ct);

        Assert.Equal(ServiceErrorType.Validation, badKey.ErrorType);
        Assert.Equal(ServiceErrorType.Validation, badValue.ErrorType);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Upsert_without_changes_does_not_save_or_audit()
    {
        var result = await _service.UpsertEnvironmentVariablesAsync(_project.Id, new Dictionary<string, string> { ["API_KEY"] = "old" }, overwrite: true, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.HasChanges);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
        await _auditLog.DidNotReceiveWithAnyArgs().LogAsync(default!, Ct);
    }

    [Fact]
    public async Task Upsert_on_a_project_without_environment_creates_it()
    {
        _project.EncryptedEnvironment = null;

        var result = await _service.UpsertEnvironmentVariablesAsync(_project.Id, new Dictionary<string, string> { ["A"] = "1" }, false, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("A=1\n", Stored());
    }

    [Fact]
    public async Task Keys_are_listed_in_file_order()
    {
        var keys = await _service.GetEnvironmentKeysAsync(_project.Id, Ct);

        Assert.Equal(["DATABASE_URL", "API_KEY"], keys.Data);
    }

    [Fact]
    public async Task Reveal_returns_one_value_and_is_audited()
    {
        var value = await _service.RevealValueAsync(_project.Id, "API_KEY", Ct);

        Assert.Equal("old", value.Data);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectEnvironmentReveal && e.Details == "Anahtar: API_KEY"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Set_rejects_adding_an_existing_key_and_editing_a_missing_one()
    {
        var add = await _service.SetVariableAsync(_project.Id, new EnvironmentVariableDto { Key = "API_KEY", Value = "x", IsNew = true }, Ct);
        var edit = await _service.SetVariableAsync(_project.Id, new EnvironmentVariableDto { Key = "MISSING", Value = "x", IsNew = false }, Ct);

        Assert.Equal(ServiceErrorType.Validation, add.ErrorType);
        Assert.Equal(ServiceErrorType.NotFound, edit.ErrorType);
    }

    [Fact]
    public async Task Delete_removes_the_line_and_keeps_the_comment()
    {
        var result = await _service.DeleteVariableAsync(_project.Id, "DATABASE_URL", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("# db\nAPI_KEY=old\n", Stored());
    }

    [Fact]
    public async Task Unreadable_environment_blocks_edits_but_can_be_replaced()
    {
        _project.EncryptedEnvironment = "bozuk";

        var upsert = await _service.UpsertEnvironmentVariablesAsync(_project.Id, new Dictionary<string, string> { ["A"] = "1" }, true, Ct);
        var replaced = await _service.ImportAsync(_project.Id, "A=1\nB=2", EnvironmentImportMode.Replace, Ct);

        Assert.False(upsert.IsSuccess);
        Assert.True(replaced.IsSuccess);
        Assert.Equal("A=1\nB=2\n", Stored());
    }

    [Fact]
    public async Task Replace_import_reports_removed_keys()
    {
        var result = await _service.ImportAsync(_project.Id, "API_KEY=old\nNEW=1", EnvironmentImportMode.Replace, Ct);

        Assert.Equal(["NEW"], result.Data!.Added);
        Assert.Equal(["DATABASE_URL"], result.Data.Removed);
        Assert.Empty(result.Data.Updated);
    }

    [Fact]
    public async Task Import_validates_content()
    {
        var result = await _service.ImportAsync(_project.Id, "A=1\nA=2", EnvironmentImportMode.Overwrite, Ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Export_returns_the_file_and_is_audited()
    {
        var result = await _service.ExportAsync(_project.Id, Ct);

        Assert.Equal("# db\nDATABASE_URL=postgres://db\nAPI_KEY=old\n", result.Data);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectEnvironmentExport), Arg.Any<CancellationToken>());
    }
}

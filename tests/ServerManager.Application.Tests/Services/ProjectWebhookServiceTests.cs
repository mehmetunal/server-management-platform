using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Auditing;
using ServerManager.Application.Deployments;
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

public class ProjectWebhookServiceTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private readonly IDeploymentRepository _repository = Substitute.For<IDeploymentRepository>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly FakeSecretProtector _protector = new();
    private readonly DeploymentProject _project;
    private readonly ProjectWebhookService _service;

    public ProjectWebhookServiceTests()
    {
        _project = ProjectTestData.Project(ProjectTestData.Server());
        _repository.GetProjectAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);
        _service = new ProjectWebhookService(
            _repository,
            _protector,
            _auditLog,
            Substitute.For<ICurrentUserService>(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ProjectWebhookService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WebhookDelivery Push(string secret, string branch = "main")
    {
        var body = Encoding.UTF8.GetBytes($"{{\"ref\":\"refs/heads/{branch}\",\"after\":\"{Sha}\"}}");
        var signature = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
        return new WebhookDelivery(body, GitHubEvent: "push", GitHubSignature256: signature);
    }

    private async Task<string> EnableAsync()
    {
        var enabled = await _service.SetAutoDeployAsync(_project.Id, true, Ct);
        Assert.True(enabled.IsSuccess);
        return enabled.Data!.Secret!;
    }

    [Fact]
    public async Task Enabling_generates_secret_once_and_audits()
    {
        var secret = await EnableAsync();

        Assert.True(_project.AutoDeployOnPush);
        Assert.Equal(secret, _protector.Unprotect(_project.EncryptedWebhookSecret!));
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.ProjectWebhookUpdate), Arg.Any<CancellationToken>());

        await _service.SetAutoDeployAsync(_project.Id, false, Ct);
        var again = await _service.SetAutoDeployAsync(_project.Id, true, Ct);
        Assert.Null(again.Data!.Secret);
        Assert.Equal(secret, _protector.Unprotect(_project.EncryptedWebhookSecret!));
    }

    [Fact]
    public async Task Project_without_secret_is_not_found()
    {
        var result = await _service.CheckDeliveryAsync(_project.Id, Push("x"), Ct);

        Assert.Equal(WebhookCheckStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Valid_push_to_branch_is_deployed_with_commit()
    {
        var secret = await EnableAsync();

        var result = await _service.CheckDeliveryAsync(_project.Id, Push(secret), Ct);

        Assert.Equal(WebhookCheckStatus.Deploy, result.Status);
        Assert.Equal(Sha, result.Commit);
    }

    [Fact]
    public async Task Wrong_secret_is_unauthorized_and_recorded()
    {
        await EnableAsync();

        var result = await _service.CheckDeliveryAsync(_project.Id, Push("yanlis"), Ct);

        Assert.Equal(WebhookCheckStatus.Unauthorized, result.Status);
        Assert.False(_project.WebhookLastDeliverySucceeded);
        Assert.NotNull(_project.WebhookLastDeliveryAt);
    }

    [Fact]
    public async Task Other_branch_is_ignored_and_disabled_project_is_rejected()
    {
        var secret = await EnableAsync();

        var ignored = await _service.CheckDeliveryAsync(_project.Id, Push(secret, "develop"), Ct);
        await _service.SetAutoDeployAsync(_project.Id, false, Ct);
        var disabled = await _service.CheckDeliveryAsync(_project.Id, Push(secret), Ct);

        Assert.Equal(WebhookCheckStatus.Ignored, ignored.Status);
        Assert.Equal(WebhookCheckStatus.Disabled, disabled.Status);
    }

    [Fact]
    public async Task Regenerated_secret_invalidates_the_old_one()
    {
        var old = await EnableAsync();

        var regenerated = await _service.RegenerateSecretAsync(_project.Id, Ct);

        Assert.NotEqual(old, regenerated.Data!.Secret);
        Assert.Equal(WebhookCheckStatus.Unauthorized, (await _service.CheckDeliveryAsync(_project.Id, Push(old), Ct)).Status);
        Assert.Equal(WebhookCheckStatus.Deploy, (await _service.CheckDeliveryAsync(_project.Id, Push(regenerated.Data.Secret!), Ct)).Status);
    }
}

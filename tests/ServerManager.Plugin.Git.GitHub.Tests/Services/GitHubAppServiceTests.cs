using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;
using ServerManager.Plugin.Git.GitHub.Services;
using ServerManager.Plugin.Git.GitHub.Tests.Fakes;
using ServerManager.Plugin.Git.GitHub.Validators;

namespace ServerManager.Plugin.Git.GitHub.Tests.Services;

public class GitHubAppServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IGitHubAppRepository _repository = Substitute.For<IGitHubAppRepository>();
    private readonly IGitHubApiClient _api = Substitute.For<IGitHubApiClient>();
    private readonly IGitHubAppGateway _gateway = Substitute.For<IGitHubAppGateway>();
    private readonly FakeSecretProtector _protector = new();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly GitHubAppService _service;

    public GitHubAppServiceTests()
    {
        _currentUser.UserId.Returns("u1");
        _currentUser.UserName.Returns("admin@example.com");
        _repository.GetProjectNamesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _service = new GitHubAppService(_repository, _api, _gateway, _protector, _auditLog, _currentUser, _cache,
            new GitHubManifestRequestDtoValidator(), new GitHubManualAppDtoValidator(),
            Options.Create(new GitHubOptions { WebUrl = "https://github.test" }),
            new FixedTimeProvider(Now), NullLogger<GitHubAppService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string StateFrom(string postUrl) => postUrl[(postUrl.IndexOf("state=", StringComparison.Ordinal) + 6)..];

    private static GitHubManifestConversion Conversion() =>
        new(77, "server-manager", "Server Manager", "Iv1.x", "client-secret", "hook-secret", "PEM-CONTENT", "acme", "https://github.test/apps/server-manager");

    [Fact]
    public async Task Manifest_flow_saves_encrypted_secrets_and_returns_install_url()
    {
        var start = await _service.StartManifestAsync(new GitHubManifestRequestDto { Name = "Server Manager" }, "https://panel.test", Ct);
        Assert.StartsWith("https://github.test/settings/apps/new?state=", start.Data!.PostUrl);
        _api.ConvertManifestAsync("code-1", Arg.Any<CancellationToken>()).Returns(ServiceResult<GitHubManifestConversion>.Success(Conversion()));
        GitHubApp? added = null;
        await _repository.AddAsync(Arg.Do<GitHubApp>(a => added = a), Arg.Any<CancellationToken>());

        var result = await _service.CompleteManifestAsync("code-1", StateFrom(start.Data.PostUrl), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://github.test/apps/server-manager/installations/new", result.Data);
        Assert.NotNull(added);
        Assert.Equal(77, added.AppId);
        Assert.Equal("PEM-CONTENT", _protector.Unprotect(added.EncryptedPrivateKey));
        Assert.Equal("client-secret", _protector.Unprotect(added.EncryptedClientSecret!));
        Assert.Equal("admin@example.com", added.CreatedBy);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == GitHubAuditActions.AppCreate && !e.Details!.Contains("PEM-CONTENT") && !e.Details.Contains("client-secret")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manifest_state_is_single_use_and_bound_to_user()
    {
        var start = await _service.StartManifestAsync(new GitHubManifestRequestDto { Name = "SM" }, "https://panel.test", Ct);
        var state = StateFrom(start.Data!.PostUrl);

        _currentUser.UserId.Returns("someone-else");
        var otherUser = await _service.CompleteManifestAsync("code", state, Ct);
        Assert.Equal(ServiceErrorType.Forbidden, otherUser.ErrorType);

        _currentUser.UserId.Returns("u1");
        _api.ConvertManifestAsync("code", Arg.Any<CancellationToken>()).Returns(ServiceResult<GitHubManifestConversion>.Success(Conversion()));
        Assert.True((await _service.CompleteManifestAsync("code", state, Ct)).IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, (await _service.CompleteManifestAsync("code", state, Ct)).ErrorType);
        await _api.Received(1).ConvertManifestAsync("code", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_state_never_reaches_github()
    {
        var result = await _service.CompleteManifestAsync("code", "forged", Ct);

        Assert.False(result.IsSuccess);
        await _api.DidNotReceiveWithAnyArgs().ConvertManifestAsync(default!, Ct);
    }

    [Fact]
    public async Task Manual_add_verifies_key_against_github()
    {
        _gateway.VerifyAsync(5, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitHubAppInfo>.Success(new GitHubAppInfo(5, "sm", "SM", "acme", null)));
        GitHubApp? added = null;
        await _repository.AddAsync(Arg.Do<GitHubApp>(a => added = a), Arg.Any<CancellationToken>());

        var result = await _service.AddManualAsync(new GitHubManualAppDto { AppId = 5, PrivateKey = TestKeys.PrivateKeyPem + "\n" }, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("sm", added!.Slug);
        Assert.Equal(TestKeys.PrivateKeyPem.Trim(), _protector.Unprotect(added.EncryptedPrivateKey));
    }

    [Fact]
    public async Task Manual_add_rejects_invalid_key_and_duplicates()
    {
        var invalid = await _service.AddManualAsync(new GitHubManualAppDto { AppId = 5, PrivateKey = "nope" }, Ct);
        Assert.Contains(invalid.Errors, e => e.PropertyName == nameof(GitHubManualAppDto.PrivateKey));

        _repository.ExistsByAppIdAsync(5, Arg.Any<CancellationToken>()).Returns(true);
        var duplicate = await _service.AddManualAsync(new GitHubManualAppDto { AppId = 5, PrivateKey = TestKeys.PrivateKeyPem }, Ct);
        Assert.Contains(duplicate.Errors, e => e.PropertyName == nameof(GitHubManualAppDto.AppId));
        await _gateway.DidNotReceiveWithAnyArgs().VerifyAsync(default, default!, Ct);
    }

    [Fact]
    public async Task Delete_is_blocked_while_projects_use_the_app()
    {
        var app = new GitHubApp { Name = "SM", AppId = 5, Slug = "sm", EncryptedPrivateKey = "x" };
        _repository.GetAsync(app.Id, Arg.Any<CancellationToken>()).Returns(app);
        _repository.GetProjectNamesAsync(app.Id, Arg.Any<CancellationToken>()).Returns(["Müşteri API"]);

        var result = await _service.DeleteAsync(app.Id, Ct);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Contains("Müşteri API", result.Message);
        Assert.False(app.IsDeleted);
    }

    [Fact]
    public async Task Delete_soft_deletes_invalidates_cache_and_audits()
    {
        var app = new GitHubApp { Name = "SM", AppId = 5, Slug = "sm", EncryptedPrivateKey = "x" };
        _repository.GetAsync(app.Id, Arg.Any<CancellationToken>()).Returns(app);

        var result = await _service.DeleteAsync(app.Id, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(app.IsDeleted);
        Assert.Equal(Now.UtcDateTime, app.DeletedAt);
        _gateway.Received(1).Invalidate(app.Id);
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == GitHubAuditActions.AppDelete), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", null, "Name")]
    [InlineData("Bu ad otuz dört karakterden daha uzundur", null, "Name")]
    [InlineData("SM<script>", null, "Name")]
    [InlineData("SM", "-bad", "Organization")]
    [InlineData("SM", "acme/x", "Organization")]
    public async Task Manifest_request_is_validated(string name, string? organization, string property)
    {
        var result = await _service.StartManifestAsync(new GitHubManifestRequestDto { Name = name, Organization = organization }, "https://panel.test", Ct);

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }
}

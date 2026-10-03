using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;
using ServerManager.Plugin.Git.GitHub.Integration;
using ServerManager.Plugin.Git.GitHub.Services;

namespace ServerManager.Plugin.Git.GitHub.Tests.Integration;

public class GitHubGitIntegrationTests
{
    private readonly IGitHubAppRepository _repository = Substitute.For<IGitHubAppRepository>();
    private readonly IGitHubAppGateway _gateway = Substitute.For<IGitHubAppGateway>();
    private readonly GitHubApp _app = new() { Name = "SM", AppId = 5, Slug = "sm", EncryptedPrivateKey = "x" };
    private readonly GitHubGitIntegration _integration;
    private readonly string _sourceId;

    public GitHubGitIntegrationTests()
    {
        _integration = new GitHubGitIntegration(_repository, _gateway, NullLogger<GitHubGitIntegration>.Instance);
        _sourceId = GitHubSourceIds.Format(_app.Id, 42);
        _repository.GetAsync(_app.Id, Arg.Any<CancellationToken>()).Returns(_app);
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([_app]);
        _gateway.ListRepositoriesAsync(_app, 42, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>.Success([new GitHubRepositoryInfo("Acme/Api", "https://github.com/Acme/Api.git", "main", true, null)]));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sources_skip_suspended_installations()
    {
        _gateway.ListInstallationsAsync(_app, true, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<GitHubInstallationInfo>>.Success([
                new GitHubInstallationInfo(42, "acme", "Organization", "all", null, false),
                new GitHubInstallationInfo(43, "old", "User", "all", null, true)
            ]));

        var result = await _integration.ListSourcesAsync(Ct);

        var source = Assert.Single(result.Data!);
        Assert.Equal(_sourceId, source.Id);
        Assert.Contains("acme", source.Name);
    }

    [Fact]
    public async Task Sources_fail_only_when_every_app_fails()
    {
        _gateway.ListInstallationsAsync(_app, true, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<GitHubInstallationInfo>>.Failure("kimlik hatası"));

        var result = await _integration.ListSourcesAsync(Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("kimlik hatası", result.Message);
    }

    [Fact]
    public async Task Repository_must_be_part_of_installation()
    {
        var found = await _integration.GetRepositoryAsync(_sourceId, "acme/api", Ct);
        var missing = await _integration.GetRepositoryAsync(_sourceId, "acme/public-but-not-installed", Ct);

        Assert.Equal("Acme/Api", found.Data!.FullName);
        Assert.False(missing.IsSuccess);
        await _gateway.DidNotReceiveWithAnyArgs().ListBranchesAsync(default!, default, default!, Ct);
    }

    [Fact]
    public async Task Branches_are_not_requested_for_unknown_repository()
    {
        var result = await _integration.ListBranchesAsync(_sourceId, "other/repo", Ct);

        Assert.False(result.IsSuccess);
        await _gateway.DidNotReceiveWithAnyArgs().ListBranchesAsync(default!, default, default!, Ct);
    }

    [Fact]
    public async Task Access_token_uses_github_token_username()
    {
        _gateway.CreateRepositoryTokenAsync(_app, 42, "Acme/Api", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitHubInstallationToken>.Success(new GitHubInstallationToken("ghs_x", DateTimeOffset.UtcNow.AddHours(1))));

        var result = await _integration.CreateAccessTokenAsync(_sourceId, "Acme/Api", Ct);

        Assert.Equal("x-access-token", result.Data!.Username);
        Assert.Equal("ghs_x", result.Data.Token);
    }

    [Fact]
    public async Task Deleted_or_malformed_source_is_not_found()
    {
        var deleted = await _integration.ListRepositoriesAsync(GitHubSourceIds.Format(Guid.NewGuid(), 42), Ct);
        var malformed = await _integration.CreateAccessTokenAsync("bad", "acme/api", Ct);

        Assert.Equal(ServiceErrorType.NotFound, deleted.ErrorType);
        Assert.Equal(ServiceErrorType.NotFound, malformed.ErrorType);
    }
}

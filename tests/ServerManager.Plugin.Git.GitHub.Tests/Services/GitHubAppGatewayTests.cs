using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;
using ServerManager.Plugin.Git.GitHub.Services;
using ServerManager.Plugin.Git.GitHub.Tests.Fakes;

namespace ServerManager.Plugin.Git.GitHub.Tests.Services;

public class GitHubAppGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IGitHubApiClient _api = Substitute.For<IGitHubApiClient>();
    private readonly FakeSecretProtector _protector = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly GitHubAppGateway _gateway;
    private readonly GitHubApp _app;

    public GitHubAppGatewayTests()
    {
        _gateway = new GitHubAppGateway(_api, _protector, _cache, Options.Create(new GitHubOptions { ListCacheSeconds = 60 }),
            new FixedTimeProvider(Now), NullLogger<GitHubAppGateway>.Instance);
        _app = new GitHubApp { Name = "SM", AppId = 5, Slug = "sm", EncryptedPrivateKey = _protector.Protect(TestKeys.PrivateKeyPem) };
        _api.CreateInstallationTokenAsync(Arg.Any<string>(), Arg.Is(42L), Arg.Is<string?>(name => name == null), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitHubInstallationToken>.Success(new GitHubInstallationToken("ghs_list", Now.AddHours(1))));
        _api.ListRepositoriesAsync("ghs_list", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>.Success([new GitHubRepositoryInfo("acme/api", "https://github.com/acme/api.git", "main", true, null)]));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Listing_token_and_repositories_are_cached()
    {
        await _gateway.ListRepositoriesAsync(_app, 42, Ct);
        await _gateway.ListRepositoriesAsync(_app, 42, Ct);
        _api.ListBranchesAsync("ghs_list", "acme/api", Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<string>>.Success(["main"]));
        await _gateway.ListBranchesAsync(_app, 42, "acme/api", Ct);

        await _api.Received(1).CreateInstallationTokenAsync(Arg.Any<string>(), Arg.Is(42L), Arg.Is<string?>(name => name == null), Arg.Any<CancellationToken>());
        await _api.Received(1).ListRepositoriesAsync("ghs_list", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_drops_cached_lists()
    {
        await _gateway.ListRepositoriesAsync(_app, 42, Ct);
        _gateway.Invalidate(_app.Id);
        await _gateway.ListRepositoriesAsync(_app, 42, Ct);

        await _api.Received(2).ListRepositoriesAsync("ghs_list", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repository_token_is_scoped_and_never_cached()
    {
        _api.CreateInstallationTokenAsync(Arg.Any<string>(), Arg.Is(42L), Arg.Is("api"), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<GitHubInstallationToken>.Success(new GitHubInstallationToken("ghs_repo", Now.AddHours(1))));

        var first = await _gateway.CreateRepositoryTokenAsync(_app, 42, "acme/api", Ct);
        await _gateway.CreateRepositoryTokenAsync(_app, 42, "acme/api", Ct);

        Assert.Equal("ghs_repo", first.Data!.Token);
        await _api.Received(2).CreateInstallationTokenAsync(Arg.Any<string>(), Arg.Is(42L), Arg.Is("api"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unreadable_private_key_fails_without_calling_github()
    {
        _app.EncryptedPrivateKey = "corrupted";

        var result = await _gateway.ListInstallationsAsync(_app, useCache: false, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("çözülemedi", result.Message);
        await _api.DidNotReceiveWithAnyArgs().ListInstallationsAsync(default!, Ct);
    }
}

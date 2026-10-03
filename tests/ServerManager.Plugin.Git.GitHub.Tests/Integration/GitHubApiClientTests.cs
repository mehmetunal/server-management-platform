using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Integration;
using ServerManager.Plugin.Git.GitHub.Tests.Fakes;

namespace ServerManager.Plugin.Git.GitHub.Tests.Integration;

public class GitHubApiClientTests
{
    private readonly StubHttpHandler _handler = new();
    private readonly GitHubApiClient _client;

    public GitHubApiClientTests()
    {
        _client = new GitHubApiClient(
            new StubHttpClientFactory(_handler),
            Options.Create(new GitHubOptions { ApiUrl = "https://api.github.test/" }),
            NullLogger<GitHubApiClient>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sends_github_headers_and_bearer_token()
    {
        _handler.Respond("GET", "/app", """{"id": 5, "slug": "sm", "name": "SM"}""");

        var result = await _client.GetAppAsync("jwt-value", Ct);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("Bearer jwt-value", request.Authorization);
        Assert.Equal("2022-11-28", request.ApiVersion);
        Assert.Contains("ServerManager", request.UserAgent);
    }

    [Fact]
    public async Task Repository_token_is_limited_to_one_repository_and_read_access()
    {
        _handler.Respond("POST", "/app/installations/42/access_tokens", """{"token": "ghs_x", "expires_at": "2026-10-04T13:00:00Z"}""", HttpStatusCode.Created);

        var result = await _client.CreateInstallationTokenAsync("jwt", 42, "api", Ct);

        Assert.True(result.IsSuccess);
        using var body = JsonDocument.Parse(_handler.Requests.Single().Body!);
        Assert.Equal(["api"], body.RootElement.GetProperty("repositories").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("read", body.RootElement.GetProperty("permissions").GetProperty("contents").GetString());
    }

    [Fact]
    public async Task Repository_outside_installation_reports_access_error()
    {
        _handler.Respond("POST", "/app/installations/42/access_tokens", """{"message":"There is at least one repository that does not exist"}""", HttpStatusCode.UnprocessableEntity);

        var result = await _client.CreateInstallationTokenAsync("jwt", 42, "secret-repo", Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("erişilebilir değil", result.Message);
    }

    [Fact]
    public async Task Unauthorized_is_mapped_to_forbidden()
    {
        _handler.Respond("GET", "/app", """{"message":"Bad credentials"}""", HttpStatusCode.Unauthorized);

        var result = await _client.GetAppAsync("jwt", Ct);

        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        Assert.DoesNotContain("jwt", result.Message);
    }

    [Fact]
    public async Task Repositories_are_paginated_and_sorted()
    {
        var firstPage = new StringBuilder("""{"total_count": 101, "repositories": [""");
        for (var i = 0; i < 100; i++)
            firstPage.Append(i == 0 ? "" : ",").Append($$"""{"full_name": "acme/r{{i:D3}}", "clone_url": "https://github.com/acme/r{{i:D3}}.git"}""");
        firstPage.Append("]}");
        _handler.Respond("GET", "/installation/repositories?per_page=100&page=1", firstPage.ToString());
        _handler.Respond("GET", "/installation/repositories?per_page=100&page=2",
            """{"total_count": 101, "repositories": [{"full_name": "acme/a-first", "clone_url": "https://github.com/acme/a-first.git"}]}""");

        var result = await _client.ListRepositoriesAsync("ghs", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(101, result.Data!.Count);
        Assert.Equal("acme/a-first", result.Data[0].FullName);
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Branch_path_escapes_repository_segments()
    {
        _handler.Respond("GET", "/repos/acme/my%20repo/branches?per_page=100&page=1", """[{"name":"main"}]""");

        var result = await _client.ListBranchesAsync("ghs", "acme/my repo", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["main"], result.Data);
    }

    [Fact]
    public async Task Manifest_conversion_is_unauthenticated_and_expired_code_is_explained()
    {
        var result = await _client.ConvertManifestAsync("old-code", Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("süresi dolmuş", result.Message);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/app-manifests/old-code/conversions", request.PathAndQuery);
        Assert.Null(request.Authorization);
    }

    [Fact]
    public async Task Invalid_api_url_fails_without_request()
    {
        var client = new GitHubApiClient(new StubHttpClientFactory(_handler), Options.Create(new GitHubOptions { ApiUrl = "ftp://x" }), NullLogger<GitHubApiClient>.Instance);

        var result = await client.GetAppAsync("jwt", Ct);

        Assert.False(result.IsSuccess);
        Assert.Empty(_handler.Requests);
    }
}

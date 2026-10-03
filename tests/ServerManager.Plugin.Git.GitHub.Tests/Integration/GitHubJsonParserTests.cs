using ServerManager.Plugin.Git.GitHub.Integration;

namespace ServerManager.Plugin.Git.GitHub.Tests.Integration;

public class GitHubJsonParserTests
{
    [Fact]
    public void Parses_installations_and_marks_suspended()
    {
        var installations = GitHubJsonParser.ParseInstallations("""
            [
              {"id": 11, "account": {"login": "acme", "type": "Organization"}, "repository_selection": "all", "html_url": "https://github.com/organizations/acme/settings/installations/11", "suspended_at": null},
              {"id": 12, "account": {"login": "mehmet", "type": "User"}, "repository_selection": "selected", "suspended_at": "2026-01-01T00:00:00Z"},
              {"account": {"login": "broken"}}
            ]
            """);

        Assert.NotNull(installations);
        Assert.Equal(2, installations.Count);
        Assert.Equal("acme", installations[0].AccountLogin);
        Assert.Equal("Organization", installations[0].AccountType);
        Assert.False(installations[0].IsSuspended);
        Assert.True(installations[1].IsSuspended);
    }

    [Fact]
    public void Parses_repository_page()
    {
        var page = GitHubJsonParser.ParseRepositoryPage("""
            {"total_count": 2, "repositories": [
              {"full_name": "acme/api", "clone_url": "https://github.com/acme/api.git", "default_branch": "develop", "private": true, "html_url": "https://github.com/acme/api"},
              {"full_name": "acme/web", "clone_url": "https://github.com/acme/web.git"}
            ]}
            """);

        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal("develop", page.Repositories[0].DefaultBranch);
        Assert.True(page.Repositories[0].IsPrivate);
        Assert.Equal("main", page.Repositories[1].DefaultBranch);
        Assert.False(page.Repositories[1].IsPrivate);
    }

    [Fact]
    public void Parses_conversion_with_secrets()
    {
        var conversion = GitHubJsonParser.ParseConversion("""
            {"id": 77, "slug": "server-manager", "name": "Server Manager", "owner": {"login": "acme"},
             "client_id": "Iv1.x", "client_secret": "cs", "webhook_secret": null, "pem": "-----BEGIN RSA PRIVATE KEY-----", "html_url": "https://github.com/apps/server-manager"}
            """);

        Assert.NotNull(conversion);
        Assert.Equal(77, conversion.Id);
        Assert.Equal("acme", conversion.OwnerLogin);
        Assert.Equal("cs", conversion.ClientSecret);
        Assert.Null(conversion.WebhookSecret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"id": "x", "slug": "a"}""")]
    [InlineData("""{"id": 1, "slug": "a"}""")]
    public void Conversion_without_required_fields_is_rejected(string json) =>
        Assert.Null(GitHubJsonParser.ParseConversion(json));

    [Fact]
    public void Parses_token_and_branches()
    {
        var token = GitHubJsonParser.ParseToken("""{"token": "ghs_x", "expires_at": "2026-10-04T13:00:00Z"}""");
        Assert.NotNull(token);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 13, 0, 0, TimeSpan.Zero), token.ExpiresAt);

        Assert.Equal(["main", "dev"], GitHubJsonParser.ParseBranches("""[{"name":"main"},{"name":"dev"},{"name":""},{}]"""));
        Assert.Null(GitHubJsonParser.ParseBranches("""{"message":"x"}"""));
    }
}

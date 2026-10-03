using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class GitSourceKeysTests
{
    [Fact]
    public void Format_and_parse_round_trip()
    {
        var key = GitSourceKeys.Format("Git.GitHub", "0f8fad5bd9cb469fa165708e7c5b1a2b:42");

        Assert.True(GitSourceKeys.TryParse(key, out var systemName, out var sourceId));
        Assert.Equal("Git.GitHub", systemName);
        Assert.Equal("0f8fad5bd9cb469fa165708e7c5b1a2b:42", sourceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Git.GitHub")]
    [InlineData("|abc")]
    [InlineData("Git.GitHub|")]
    [InlineData("Git/GitHub|abc")]
    [InlineData("Git.GitHub|a b")]
    [InlineData("Git.GitHub|a|b")]
    [InlineData("Git.GitHub|../x")]
    public void Rejects_malformed_keys(string? key) =>
        Assert.False(GitSourceKeys.TryParse(key, out _, out _));

    [Theory]
    [InlineData("acme/api")]
    [InlineData("acme-inc/api.service")]
    [InlineData("group/sub/repo")]
    public void Accepts_repository_names(string repository) =>
        Assert.True(GitSourceKeys.IsValidRepository(repository));

    [Theory]
    [InlineData(null)]
    [InlineData("api")]
    [InlineData("acme/")]
    [InlineData("/api")]
    [InlineData("acme/../api")]
    [InlineData("acme/..")]
    [InlineData("./api")]
    [InlineData("acme/api?x=1")]
    [InlineData("acme/api name")]
    public void Rejects_invalid_repository_names(string? repository) =>
        Assert.False(GitSourceKeys.IsValidRepository(repository));
}

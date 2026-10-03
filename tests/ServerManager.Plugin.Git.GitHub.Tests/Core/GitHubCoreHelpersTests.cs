using System.Text.Json;
using ServerManager.Application.Deployments;
using ServerManager.Plugin.Git.GitHub.Core;

namespace ServerManager.Plugin.Git.GitHub.Tests.Core;

public class GitHubCoreHelpersTests
{
    [Fact]
    public void Source_ids_round_trip_and_fit_core_key_format()
    {
        var appId = Guid.NewGuid();
        var sourceId = GitHubSourceIds.Format(appId, 98765);

        Assert.True(GitHubSourceIds.TryParse(sourceId, out var parsedApp, out var installation));
        Assert.Equal(appId, parsedApp);
        Assert.Equal(98765, installation);
        Assert.StartsWith(GitHubSourceIds.Prefix(appId), sourceId);
        Assert.True(GitSourceKeys.TryParse(GitSourceKeys.Format(GitHubPlugin.SystemName, sourceId), out _, out var roundTrip));
        Assert.Equal(sourceId, roundTrip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc:1")]
    [InlineData("0f8fad5bd9cb469fa165708e7c5b1a2b")]
    [InlineData("0f8fad5bd9cb469fa165708e7c5b1a2b:")]
    [InlineData("0f8fad5bd9cb469fa165708e7c5b1a2b:0")]
    [InlineData("0f8fad5bd9cb469fa165708e7c5b1a2b:-5")]
    [InlineData("0f8fad5bd9cb469fa165708e7c5b1a2b:1x")]
    public void Rejects_malformed_source_ids(string? sourceId) =>
        Assert.False(GitHubSourceIds.TryParse(sourceId, out _, out _));

    [Fact]
    public void Builds_install_and_manifest_urls()
    {
        Assert.Equal("https://github.com/apps/server-manager/installations/new", GitHubUrls.InstallUrl("https://github.com/", "server-manager"));
        Assert.Equal("https://github.com/settings/apps/new?state=abc", GitHubUrls.ManifestPostUrl("https://github.com", null, "abc"));
        Assert.Equal("https://github.com/organizations/acme-inc/settings/apps/new?state=abc", GitHubUrls.ManifestPostUrl("https://github.com", " acme-inc ", "abc"));
    }

    [Fact]
    public void Repository_helpers_escape_segments()
    {
        Assert.Equal("acme/api.service", GitHubUrls.RepositoryPath("acme/api.service"));
        Assert.Equal("api", GitHubUrls.RepositoryName("acme/api"));
    }

    [Fact]
    public void Manifest_requests_read_only_permissions_and_returns_to_panel()
    {
        using var manifest = JsonDocument.Parse(GitHubManifest.Build(" Server Manager ", "https://panel.example.com/"));
        var root = manifest.RootElement;

        Assert.Equal("Server Manager", root.GetProperty("name").GetString());
        Assert.Equal("https://panel.example.com", root.GetProperty("url").GetString());
        Assert.Equal("https://panel.example.com/GitHub/Callback", root.GetProperty("redirect_url").GetString());
        Assert.Equal("https://panel.example.com/GitHub/Setup", root.GetProperty("setup_url").GetString());
        Assert.False(root.GetProperty("public").GetBoolean());
        Assert.False(root.TryGetProperty("hook_attributes", out _));

        var permissions = root.GetProperty("default_permissions").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        Assert.Equal(new Dictionary<string, string?> { ["contents"] = "read", ["metadata"] = "read" }, permissions);
    }
}

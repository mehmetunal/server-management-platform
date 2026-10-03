using ServerManager.Application.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Deployments;

public class GitRepositoryUrlsTests
{
    [Theory]
    [InlineData("https://github.com/acme/api.git")]
    [InlineData("http://git.local/acme/api")]
    [InlineData("ssh://git@git.local:2222/acme/api.git")]
    [InlineData("git@github.com:acme/api.git")]
    [InlineData("file:///srv/git/api.git")]
    public void Accepts_supported_addresses(string url) => Assert.True(GitRepositoryUrls.TryValidate(url, out _));

    [Theory]
    [InlineData("", "zorunludur")]
    [InlineData("https://user:token@github.com/acme/api.git", "anahtar yazmayın")]
    [InlineData("https://github.com", "Depo yolunu")]
    [InlineData("ssh://git:secret@git.local/acme/api.git", "parola")]
    [InlineData("ftp://git.local/acme/api", "Desteklenen")]
    [InlineData("https://github.com/acme/api.git; rm -rf /", "boşluk")]
    [InlineData("https://github.com/acme/'api'", "tırnak")]
    [InlineData("-uhttps://evil", "Geçerli")]
    public void Rejects_unsafe_or_unsupported_addresses(string url, string fragment)
    {
        Assert.False(GitRepositoryUrls.TryValidate(url, out var error));
        Assert.Contains(fragment, error);
    }

    [Theory]
    [InlineData("https://github.com/acme/api.git", "https://github.com/acme/other.git", true)]
    [InlineData("https://github.com/acme/api.git", "https://GITHUB.com/acme/api", true)]
    [InlineData("https://github.com/acme/api.git", "https://github.com.evil.example/acme/api.git", false)]
    [InlineData("https://github.com/acme/api.git", "http://github.com/acme/api.git", false)]
    [InlineData("https://git.local:8443/a/b.git", "https://git.local/a/b.git", false)]
    [InlineData("git@github.com:acme/api.git", "https://github.com/acme/api.git", false)]
    public void Same_host_check_compares_scheme_host_and_port(string first, string second, bool expected) =>
        Assert.Equal(expected, GitRepositoryUrls.HaveSameHost(first, second));

    [Theory]
    [InlineData(GitProvider.GitHub, null, "x-access-token")]
    [InlineData(GitProvider.GitLab, "", "oauth2")]
    [InlineData(GitProvider.Bitbucket, null, "x-token-auth")]
    [InlineData(GitProvider.SelfHosted, null, "git")]
    [InlineData(GitProvider.GitHub, " ci-bot ", "ci-bot")]
    public void Token_username_defaults_per_provider(GitProvider provider, string? username, string expected) =>
        Assert.Equal(expected, GitRepositoryUrls.TokenUsername(provider, username));

    [Theory]
    [InlineData(GitProvider.GitHub, "https://github.com/acme/api.git", "https://github.com/acme/api/commit/abc")]
    [InlineData(GitProvider.GitLab, "https://gitlab.com/acme/api", "https://gitlab.com/acme/api/-/commit/abc")]
    [InlineData(GitProvider.Bitbucket, "https://bitbucket.org/acme/api.git", "https://bitbucket.org/acme/api/commits/abc")]
    [InlineData(GitProvider.GitHub, "git@github.com:acme/api.git", null)]
    public void Builds_commit_url_for_https_repositories(GitProvider provider, string url, string? expected) =>
        Assert.Equal(expected, GitRepositoryUrls.CommitUrl(provider, url, "abc"));
}

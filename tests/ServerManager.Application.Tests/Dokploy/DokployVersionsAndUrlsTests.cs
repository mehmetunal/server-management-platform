using ServerManager.Application.Dokploy;

namespace ServerManager.Application.Tests.Dokploy;

public class DokployVersionsAndUrlsTests
{
    [Theory]
    [InlineData("latest")]
    [InlineData("canary")]
    [InlineData("v0.25.3")]
    [InlineData("0.25.3")]
    [InlineData("v1.0.0-rc.1")]
    [InlineData("feature/new-ui")]
    public void Accepts_script_version_tags(string version)
    {
        Assert.True(DokployVersions.IsValid(version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v0.25")]
    [InlineData("latest; reboot")]
    [InlineData("$(id)")]
    [InlineData("feature/../x y")]
    public void Rejects_unsafe_or_unknown_versions(string? version)
    {
        Assert.False(DokployVersions.IsValid(version));
    }

    [Theory]
    [InlineData("dokploy/dokploy:v0.25.3", "v0.25.3")]
    [InlineData("dokploy/dokploy:latest@sha256:abc", "latest")]
    [InlineData("registry.local:5000/dokploy/dokploy", "latest")]
    [InlineData("registry.local:5000/dokploy/dokploy:canary", "canary")]
    [InlineData(null, null)]
    public void Parses_image_tag(string? image, string? expected)
    {
        Assert.Equal(expected, DokployVersions.ParseImageTag(image));
    }

    [Theory]
    [InlineData("203.0.113.10", "http://203.0.113.10:3000")]
    [InlineData("2001:db8::1", "http://[2001:db8::1]:3000")]
    [InlineData("panel.example.com", "http://panel.example.com:3000")]
    public void Builds_default_url(string host, string expected)
    {
        Assert.Equal(expected, DokployUrls.BuildDefault(host, 3000));
    }

    [Theory]
    [InlineData(" https://panel.example.com/ ", "https://panel.example.com")]
    [InlineData("http://203.0.113.10:3000", "http://203.0.113.10:3000")]
    [InlineData("https://example.com/dokploy/", "https://example.com/dokploy")]
    public void Normalizes_valid_urls(string value, string expected)
    {
        Assert.True(DokployUrls.TryNormalize(value, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("panel.example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://user:pass@example.com")]
    [InlineData("http://example.com/?redirect=x")]
    [InlineData("http://example.com/#x")]
    public void Rejects_invalid_urls(string? value)
    {
        Assert.False(DokployUrls.TryNormalize(value, out _));
    }

    [Fact]
    public void Reads_port_from_url()
    {
        Assert.Equal(3000, DokployUrls.GetPort("http://203.0.113.10:3000"));
        Assert.Equal(443, DokployUrls.GetPort("https://panel.example.com"));
        Assert.Null(DokployUrls.GetPort("not a url"));
    }
}

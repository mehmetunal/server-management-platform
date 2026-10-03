using ServerManager.Application.Docker;

namespace ServerManager.Application.Tests.Docker;

public class DockerNamesTests
{
    [Theory]
    [InlineData("web")]
    [InlineData("a")]
    [InlineData("shop-web-1")]
    [InlineData("my_app.v2")]
    [InlineData("7d0dac637d87")]
    public void Accepts_valid_container_references(string value)
    {
        Assert.True(DockerNames.IsValidContainerReference(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-web")]
    [InlineData(".web")]
    [InlineData("web; rm -rf /")]
    [InlineData("web$(id)")]
    [InlineData("web`id`")]
    [InlineData("we'b")]
    [InlineData("web name")]
    [InlineData("web\nid")]
    [InlineData("ağ")]
    public void Rejects_unsafe_container_references(string? value)
    {
        Assert.False(DockerNames.IsValidContainerReference(value));
    }

    [Fact]
    public void Rejects_names_longer_than_limit()
    {
        Assert.False(DockerNames.IsValidContainerReference(new string('a', DockerNames.MaxLength + 1)));
        Assert.True(DockerNames.IsValidContainerReference(new string('a', DockerNames.MaxLength)));
    }

    [Fact]
    public void New_names_require_at_least_two_characters()
    {
        Assert.False(DockerNames.IsValidContainerName("a"));
        Assert.True(DockerNames.IsValidContainerName("ab"));
        Assert.False(DockerNames.IsValidVolumeName("v"));
        Assert.False(DockerNames.IsValidNetworkName("n"));
    }

    [Theory]
    [InlineData("nginx")]
    [InlineData("nginx:alpine")]
    [InlineData("ghcr.io/org/app:1.2.0")]
    [InlineData("localhost:5000/team/app")]
    [InlineData("nginx@sha256:df221db836e1754089190208cee7eeda94f233197056426eda74a43ab1abeac2")]
    [InlineData("sha256:a2b80c421aaa02ba1ef4ef2d3c991112674e676f9db05fd42d456f2529b83a86")]
    public void Accepts_valid_image_references(string value)
    {
        Assert.True(DockerNames.IsValidImageReference(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/nginx")]
    [InlineData("nginx alpine")]
    [InlineData("nginx;id")]
    [InlineData("../etc/passwd")]
    [InlineData("org/../app")]
    [InlineData("--help")]
    public void Rejects_invalid_image_references(string value)
    {
        Assert.False(DockerNames.IsValidImageReference(value));
    }

    [Theory]
    [InlineData("172.30.0.0/16", true)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("192.168.1.0/30", true)]
    [InlineData("192.168.1.0/31", false)]
    [InlineData("10.0.0.0/7", false)]
    [InlineData("172.30.0.0", false)]
    [InlineData("fd00::/64", false)]
    [InlineData("abc/16", false)]
    public void Validates_subnets(string value, bool expected)
    {
        Assert.Equal(expected, DockerNames.IsValidSubnet(value));
    }

    [Theory]
    [InlineData("172.30.0.1", true)]
    [InlineData("172.30.1", false)]
    [InlineData("::1", false)]
    [InlineData("", false)]
    public void Validates_ipv4(string value, bool expected)
    {
        Assert.Equal(expected, DockerNames.IsValidIpv4(value));
    }

    [Theory]
    [InlineData("bridge", true)]
    [InlineData("overlay", true)]
    [InlineData("host", false)]
    [InlineData("Bridge", false)]
    [InlineData(null, false)]
    public void Validates_network_drivers(string? value, bool expected)
    {
        Assert.Equal(expected, DockerNames.IsValidNetworkDriver(value));
    }

    [Theory]
    [InlineData("2026-10-03T18:48:19.219917720Z")]
    [InlineData("2026-10-03T18:48:19Z")]
    [InlineData("2026-10-03T21:48:19.5+03:00")]
    [InlineData("1759517299")]
    [InlineData("1759517299.219917720")]
    public void Accepts_log_since_values(string value)
    {
        Assert.True(DockerNames.IsValidLogSince(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10m")]
    [InlineData("2026-10-03")]
    [InlineData("2026-10-03T18:48:19.1234567890Z")]
    [InlineData("2026-10-03T18:48:19Z; id")]
    [InlineData("'1759517299'")]
    public void Rejects_invalid_log_since_values(string? value)
    {
        Assert.False(DockerNames.IsValidLogSince(value));
    }
}

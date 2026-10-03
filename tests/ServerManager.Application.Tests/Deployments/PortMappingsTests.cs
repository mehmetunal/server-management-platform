using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class PortMappingsTests
{
    [Fact]
    public void Parses_comma_and_space_separated_mappings()
    {
        Assert.True(PortMappings.TryParse("8080:80, 127.0.0.1:9000:9000\n5353:53/udp", out var mappings, out var error));
        Assert.Null(error);
        Assert.Equal(["8080:80", "127.0.0.1:9000:9000", "5353:53/udp"], mappings);
    }

    [Theory]
    [InlineData("80")]
    [InlineData("0:80")]
    [InlineData("70000:80")]
    [InlineData("999.1.1.1:80:80")]
    [InlineData("8080:80/sctp")]
    [InlineData("8080:80;id")]
    [InlineData("--privileged")]
    public void Rejects_invalid_mappings(string text)
    {
        Assert.False(PortMappings.TryParse(text, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Limits_mapping_count() =>
        Assert.False(PortMappings.TryParse(string.Join(',', Enumerable.Range(8000, PortMappings.MaxCount + 1).Select(p => $"{p}:80")), out _, out _));
}

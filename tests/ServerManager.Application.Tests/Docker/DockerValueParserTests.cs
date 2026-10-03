using ServerManager.Infrastructure.Docker;

namespace ServerManager.Application.Tests.Docker;

public class DockerValueParserTests
{
    [Theory]
    [InlineData("0B", 0L)]
    [InlineData("89B", 89L)]
    [InlineData("1.19kB", 1190L)]
    [InlineData("62.4MB", 62_400_000L)]
    [InlineData("1.2GB (50%)", 1_200_000_000L)]
    [InlineData("5.953MiB", 6_242_173L)]
    [InlineData("7.6GiB", 8_160_437_862L)]
    [InlineData("1KiB", 1024L)]
    public void Parses_docker_sizes(string value, long expected)
    {
        Assert.Equal(expected, DockerValueParser.ParseSize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("N/A")]
    [InlineData("12 parsecs")]
    public void Unknown_sizes_return_null(string? value)
    {
        Assert.Null(DockerValueParser.ParseSize(value));
    }

    [Fact]
    public void Parses_size_pairs()
    {
        Assert.Equal((1024L, 2048L), DockerValueParser.ParseSizePair("1KiB / 2KiB"));
        Assert.Equal((1000L, 0L), DockerValueParser.ParseSizePair("1kB"));
        Assert.Equal((0L, 0L), DockerValueParser.ParseSizePair(null));
    }

    [Theory]
    [InlineData("12.5%", 12.5)]
    [InlineData(" 0.00% ", 0)]
    [InlineData("--", 0)]
    [InlineData(null, 0)]
    public void Parses_percentages(string? value, double expected)
    {
        Assert.Equal(expected, DockerValueParser.ParsePercent(value));
    }

    [Theory]
    [InlineData("2026-10-03 18:31:49 +0000 UTC", 18, 31, 49, 0L)]
    [InlineData("2026-10-03 18:31:49.693094553 +0000 UTC", 18, 31, 49, 6930945L)]
    [InlineData("2026-10-03T18:45:13.224248009Z", 18, 45, 13, 2242480L)]
    [InlineData("2026-10-03T21:45:13+03:00", 18, 45, 13, 0L)]
    [InlineData("2026-10-03 21:45:13 +0300 +03", 18, 45, 13, 0L)]
    public void Parses_timestamps_as_utc(string value, int hour, int minute, int second, long fractionTicks)
    {
        var expected = new DateTime(2026, 10, 3, hour, minute, second, DateTimeKind.Utc).AddTicks(fractionTicks);

        var parsed = DockerValueParser.ParseTimestamp(value);

        Assert.Equal(expected, parsed);
        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0001-01-01T00:00:00Z")]
    [InlineData("10 days ago")]
    public void Zero_or_invalid_timestamps_return_null(string? value)
    {
        Assert.Null(DockerValueParser.ParseTimestamp(value));
    }

    [Theory]
    [InlineData("Exited (3) 20 minutes ago", 3)]
    [InlineData("Exited (0) 4 minutes ago", 0)]
    [InlineData("Exited (137) 1 second ago", 137)]
    [InlineData("Up 6 minutes", null)]
    [InlineData(null, null)]
    public void Parses_exit_code_from_status(string? status, int? expected)
    {
        Assert.Equal(expected, DockerValueParser.ParseExitCode(status));
    }

    [Theory]
    [InlineData("Up 5 minutes (healthy)", "healthy")]
    [InlineData("Up 5 minutes (unhealthy)", "unhealthy")]
    [InlineData("Up 2 seconds (health: starting)", "starting")]
    [InlineData("Up 5 minutes", null)]
    public void Parses_health_from_status(string status, string? expected)
    {
        Assert.Equal(expected, DockerValueParser.ParseHealth(status));
    }
}

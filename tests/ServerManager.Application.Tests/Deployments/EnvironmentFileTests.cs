using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class EnvironmentFileTests
{
    [Fact]
    public void Parses_keys_and_skips_comments_and_blank_lines()
    {
        var text = "# veritabanı\r\nDATABASE_URL=postgres://u:p@db/app?x=1\r\n\r\n  API_KEY = abc=def \nEMPTY=\n";

        Assert.True(EnvironmentFile.TryParse(text, out var keys, out var error));
        Assert.Null(error);
        Assert.Equal(["DATABASE_URL", "API_KEY", "EMPTY"], keys);
    }

    [Fact]
    public void Empty_text_is_valid() => Assert.True(EnvironmentFile.TryParse("  ", out var keys, out _) && keys.Count == 0);

    [Theory]
    [InlineData("NO_SEPARATOR", "1. satır")]
    [InlineData("A=1\n9START=x", "2. satır")]
    [InlineData("export A=1", "1. satır")]
    [InlineData("A=1\nA=2", "birden fazla")]
    public void Rejects_invalid_lines(string text, string fragment)
    {
        Assert.False(EnvironmentFile.TryParse(text, out _, out var error));
        Assert.Contains(fragment, error);
    }

    [Fact]
    public void Rejects_oversized_content() =>
        Assert.False(EnvironmentFile.TryParse("A=" + new string('x', EnvironmentFile.MaxLength), out _, out _));

    [Fact]
    public void Normalize_uses_lf_and_single_trailing_newline() =>
        Assert.Equal("A=1\nB=2\n", EnvironmentFile.Normalize("A=1  \r\nB=2\r\n\r\n"));
}

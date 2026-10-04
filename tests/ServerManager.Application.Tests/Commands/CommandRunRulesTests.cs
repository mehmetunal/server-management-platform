using ServerManager.Application.Commands;

namespace ServerManager.Application.Tests.Commands;

public class CommandRunRulesTests
{
    [Fact]
    public void TruncateOutput_KeepsShortOutput()
    {
        var (text, truncated) = CommandRunRules.TruncateOutput("ok\n");

        Assert.Equal("ok\n", text);
        Assert.False(truncated);
    }

    [Fact]
    public void TruncateOutput_KeepsTailOfLongOutput()
    {
        var output = new string('a', CommandRunRules.MaxOutputChars) + "SON";

        var (text, truncated) = CommandRunRules.TruncateOutput(output);

        Assert.True(truncated);
        Assert.Equal(CommandRunRules.MaxOutputChars, text.Length);
        Assert.EndsWith("SON", text);
    }

    [Fact]
    public void TruncateOutput_HandlesNull()
    {
        Assert.Equal((string.Empty, false), CommandRunRules.TruncateOutput(null));
    }
}

using ServerManager.Application.Terminal;

namespace ServerManager.Application.Tests.Terminal;

public class TerminalInputTrackerTests
{
    private readonly TerminalInputTracker _tracker = new();

    [Fact]
    public void Submits_line_on_enter_and_reports_consumed_length()
    {
        var step = _tracker.Next("ls -la\rpwd");

        Assert.Equal(7, step.Consumed);
        Assert.NotNull(step.Submitted);
        Assert.Equal("ls -la", step.Submitted.Text);
        Assert.False(step.Submitted.IsApproximate);
        Assert.Equal(string.Empty, _tracker.CurrentLine);
    }

    [Fact]
    public void Keeps_partial_line_until_enter()
    {
        var first = _tracker.Next("echo hel");
        var second = _tracker.Next("lo\r");

        Assert.Null(first.Submitted);
        Assert.Equal(8, first.Consumed);
        Assert.Equal("echo hello", second.Submitted?.Text);
    }

    [Fact]
    public void Applies_backspace_ctrl_u_and_ctrl_w()
    {
        Assert.Equal("ls", _tracker.Next("lsx\u007f\r").Submitted?.Text);
        Assert.Equal("pwd", _tracker.Next("rm -rf /\u0015pwd\r").Submitted?.Text);
        Assert.Equal("git ", _tracker.Next("git status\u0017\r").Submitted?.Text);
    }

    [Fact]
    public void Ctrl_c_discards_current_line()
    {
        var step = _tracker.Next("rm -rf /\u0003whoami\r");

        Assert.Equal("whoami", step.Submitted?.Text);
        Assert.False(step.Submitted?.IsApproximate);
    }

    [Theory]
    [InlineData("ls\u001b[A\r")]
    [InlineData("ls\u001bOA\r")]
    [InlineData("ls\u001bb\r")]
    [InlineData("ls\t\r")]
    public void Cursor_keys_history_and_tab_mark_line_approximate(string input)
    {
        var step = _tracker.Next(input);

        Assert.Equal("ls", step.Submitted?.Text);
        Assert.True(step.Submitted?.IsApproximate);
    }

    [Fact]
    public void Bracketed_paste_keeps_newlines_inside_paste()
    {
        var step = _tracker.Next("\u001b[200~echo a\recho b\u001b[201~\r");

        Assert.Equal("echo a\necho b", step.Submitted?.Text);
        Assert.False(step.Submitted?.IsApproximate);
    }

    [Fact]
    public void Truncates_overlong_line_and_marks_it_approximate()
    {
        var step = _tracker.Next(new string('a', TerminalInputTracker.MaxLineLength + 10) + "\r");

        Assert.Equal(TerminalInputTracker.MaxLineLength, step.Submitted?.Text.Length);
        Assert.True(step.Submitted?.IsApproximate);
    }
}

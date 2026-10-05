using ServerManager.Application.Tests.Fakes;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Application.Tests.Ssh;

public class SudoPromptResponderTests
{
    private const string Marker = SudoCommandBuilder.TerminalPromptMarker;

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Answers_the_first_prompt_once_and_hides_the_marker()
    {
        var responder = new SudoPromptResponder("secret", _time);

        var text = responder.Process("banner " + Marker, out var reply);

        Assert.Equal("secret\n", reply);
        Assert.Equal("banner ", text);
    }

    [Fact]
    public void Marker_split_across_reads_is_still_recognized()
    {
        var responder = new SudoPromptResponder("secret", _time);

        var first = responder.Process("x" + Marker[..5], out var firstReply);
        var second = responder.Process(Marker[5..] + "y", out var secondReply);

        Assert.Null(firstReply);
        Assert.Equal("x", first);
        Assert.Equal("secret\n", secondReply);
        Assert.Equal("y", second);
    }

    [Fact]
    public void Second_prompt_inside_the_window_means_wrong_password()
    {
        var responder = new SudoPromptResponder("secret", _time);
        responder.Process(Marker, out _);

        Assert.Null(responder.Process(Marker, out var reply));
        Assert.Null(reply);
    }

    [Fact]
    public void Marker_printed_after_the_window_never_receives_the_password()
    {
        var responder = new SudoPromptResponder("secret", _time);
        _time.UtcNow += SudoPromptResponder.PromptWindow + TimeSpan.FromSeconds(1);

        var text = responder.Process("container log " + Marker, out var reply);

        Assert.Null(reply);
        Assert.Equal("container log " + Marker, text);
    }

    [Fact]
    public void Marker_after_the_answered_prompt_and_window_is_plain_output()
    {
        var responder = new SudoPromptResponder("secret", _time);
        responder.Process(Marker, out _);
        _time.UtcNow += SudoPromptResponder.PromptWindow;

        var text = responder.Process(Marker, out var reply);

        Assert.Null(reply);
        Assert.Equal(Marker, text);
    }

    [Fact]
    public void Without_password_output_is_forwarded_untouched()
    {
        var responder = new SudoPromptResponder(null, _time);

        Assert.Equal(Marker, responder.Process(Marker, out var reply));
        Assert.Null(reply);
    }
}

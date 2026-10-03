using ServerManager.Application.Terminal;

namespace ServerManager.Application.Tests.Terminal;

public class TerminalOutputMonitorTests
{
    private readonly TerminalOutputMonitor _monitor = new();

    [Theory]
    [InlineData("[sudo] password for deploy: ")]
    [InlineData("Enter passphrase for key '/home/deploy/.ssh/id_ed25519':")]
    [InlineData("deploy@db's Password:")]
    [InlineData("Parola: ")]
    [InlineData("\u001b[1mPassword:\u001b[0m ")]
    public void Detects_secret_prompt_on_last_line(string output)
    {
        _monitor.Inspect("deploy@web:~$ sudo ls\r\n" + output);

        Assert.True(_monitor.IsSecretPrompt);
    }

    [Fact]
    public void Secret_prompt_ends_when_new_output_arrives()
    {
        _monitor.Inspect("[sudo] password for deploy: ");
        _monitor.Inspect("\r\nfile.txt\r\ndeploy@web:~$ ");

        Assert.False(_monitor.IsSecretPrompt);
    }

    [Fact]
    public void Ordinary_output_mentioning_password_is_not_a_prompt()
    {
        _monitor.Inspect("password reset done\r\ndeploy@web:~$ ");

        Assert.False(_monitor.IsSecretPrompt);
    }

    [Fact]
    public void Tracks_alternate_screen_toggle()
    {
        _monitor.Inspect("\u001b[?1049h\u001b[H");
        Assert.True(_monitor.IsAlternateScreen);

        _monitor.Inspect("\u001b[?1049l");
        Assert.False(_monitor.IsAlternateScreen);
    }

    [Fact]
    public void Detects_alternate_screen_sequence_split_across_chunks()
    {
        _monitor.Inspect("text\u001b[?10");
        _monitor.Inspect("49h");

        Assert.True(_monitor.IsAlternateScreen);
    }
}

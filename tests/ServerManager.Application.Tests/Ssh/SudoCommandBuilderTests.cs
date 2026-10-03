using ServerManager.Application.DTOs.Ssh;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Application.Tests.Ssh;

public class SudoCommandBuilderTests
{
    private const string Command = "docker ps";

    private static RemoteExecutionContext Context(bool useSudo, string? password = null) => new()
    {
        Connection = new SshConnectionRequest { Host = "127.0.0.1", Username = "ops" },
        UseSudo = useSudo,
        SudoPassword = password
    };

    [Fact]
    public void Leaves_command_untouched_without_sudo()
    {
        Assert.Equal(Command, SudoCommandBuilder.Build(Context(false, "secret"), Command, elevate: true));
        Assert.Equal(Command, SudoCommandBuilder.Build(Context(true, "secret"), Command, elevate: false));
        Assert.False(SudoCommandBuilder.RequiresPasswordInput(Context(false, "secret"), elevate: true));
    }

    [Fact]
    public void Uses_non_interactive_sudo_without_password()
    {
        Assert.Equal("sudo -n -- docker ps", SudoCommandBuilder.Build(Context(true), Command, elevate: true));
        Assert.False(SudoCommandBuilder.RequiresPasswordInput(Context(true), elevate: true));
    }

    [Fact]
    public void Password_is_read_from_stdin_and_never_appears_in_command()
    {
        var context = Context(true, "s3cr3t'pw");

        var command = SudoCommandBuilder.Build(context, Command, elevate: true);

        Assert.Equal("sudo -S -p '' -- docker ps", command);
        Assert.DoesNotContain("s3cr3t", command);
        Assert.True(SudoCommandBuilder.RequiresPasswordInput(context, elevate: true));
    }

    [Fact]
    public void Interactive_sudo_prints_known_prompt_marker()
    {
        var command = SudoCommandBuilder.BuildInteractive(Context(true, "secret"), Command, elevate: true);

        Assert.Equal("sudo -p '[sm-sudo%%prompt]' -- docker ps", command);
        Assert.DoesNotContain(SudoCommandBuilder.TerminalPromptMarker, command);
        Assert.DoesNotContain("secret", command);
        Assert.Equal("sudo -n -- docker ps", SudoCommandBuilder.BuildInteractive(Context(true), Command, elevate: true));
    }
}

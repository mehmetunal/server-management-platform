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

        Assert.StartsWith("sh -c '", command);
        Assert.Contains("sudo -S -p", command);
        Assert.Contains("sudo -n -- docker ps", command);
        Assert.DoesNotContain("s3cr3t", command);
        Assert.True(SudoCommandBuilder.RequiresPasswordInput(context, elevate: true));
    }

    /// <summary>
    /// Sahte sudo: <c>nopasswd</c> hiç parola okumaz, <c>cache</c> -v sonrası oturumu hatırlar, <c>nocache</c> her çağrıda parola okur.
    /// Gerçek sudo gibi parola gerekmediğinde stdin'e dokunmaz; parola sızarsa komutun çıktısında görünür.
    /// </summary>
    private const string FakeSudo = """
        #!/bin/sh
        authed() { [ "$FAKE_MODE" = nopasswd ] || { [ "$FAKE_MODE" = cache ] && [ -f "$FAKE_STATE" ]; }; }
        case "$1" in
          -n)
            shift
            if [ "$1" = true ]; then authed; exit $?; fi
            [ "$1" = -- ] && shift
            authed || { echo 'sudo: a password is required' >&2; exit 1; }
            exec "$@" ;;
          -S)
            shift 3
            if ! authed; then
              IFS= read -r pw || pw=
              [ "$pw" = "$FAKE_PW" ] || { echo 'Sorry, try again.' >&2; exit 1; }
            fi
            if [ "$1" = -v ]; then
              [ "$FAKE_MODE" = cache ] && : > "$FAKE_STATE"
              exit 0
            fi
            [ "$1" = -- ] && shift
            exec "$@" ;;
        esac
        exit 64
        """;

    [Theory]
    [InlineData("nopasswd")]
    [InlineData("cache")]
    [InlineData("nocache")]
    public async Task Password_reaches_only_sudo_and_never_the_command_stdin(string mode)
    {
        if (OperatingSystem.IsWindows())
            return;

        var (exitCode, stdout, _) = await RunWithFakeSudoAsync(mode, "s3cr3t", "s3cr3t\nline-1\nline-2\n");

        Assert.Equal(0, exitCode);
        Assert.Equal("line-1\nline-2\n", stdout);
    }

    [Fact]
    public async Task Wrong_password_stops_before_the_command_runs()
    {
        if (OperatingSystem.IsWindows())
            return;

        var (exitCode, stdout, stderr) = await RunWithFakeSudoAsync("nocache", "s3cr3t", "wrong\nline-1\n");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("try again", stderr);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunWithFakeSudoAsync(string mode, string password, string input)
    {
        var directory = Directory.CreateTempSubdirectory("sm-sudo-test-");
        try
        {
            var sudo = Path.Combine(directory.FullName, "sudo");
            await File.WriteAllTextAsync(sudo, FakeSudo.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", TestContext.Current.CancellationToken);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(sudo, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var command = SudoCommandBuilder.Build(Context(true, password), "cat", elevate: true);
            var start = new System.Diagnostics.ProcessStartInfo("/bin/sh")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(command);
            start.Environment["PATH"] = directory.FullName + ":" + Environment.GetEnvironmentVariable("PATH");
            start.Environment["FAKE_MODE"] = mode;
            start.Environment["FAKE_PW"] = password;
            start.Environment["FAKE_STATE"] = Path.Combine(directory.FullName, "timestamp");

            using var process = System.Diagnostics.Process.Start(start)!;
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return (process.ExitCode, await stdout, await stderr);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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

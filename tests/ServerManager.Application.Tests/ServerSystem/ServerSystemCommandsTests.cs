using System.Diagnostics;
using ServerManager.Infrastructure.ServerSystem;

namespace ServerManager.Application.Tests.ServerSystem;

public class ServerSystemCommandsTests
{
    [Fact]
    public void Read_file_resolves_links_before_tail()
    {
        var command = ServerSystemCommands.ReadFile(100, "/var/log/syslog");

        Assert.StartsWith("sh -c '", command);
        Assert.Contains("readlink -f", command);
        Assert.Contains("tail -n 100 -- \"$r\"", command);
        Assert.True(command.IndexOf("readlink -f", StringComparison.Ordinal) < command.IndexOf("tail", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/var/log/../../etc/hosts")]
    [InlineData("/var/log/sm-test-missing-file.log")]
    [InlineData("/var/log")]
    public async Task Read_file_refuses_targets_outside_var_log_or_non_regular_files(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(ServerSystemCommands.ReadFile(10, path));
        using var process = Process.Start(start)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServerSystemCommands.LogOutsideRootExitCode, process.ExitCode);
        Assert.DoesNotContain("localhost", stdout);
    }
}

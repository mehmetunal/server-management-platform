using System.Diagnostics;
using ServerManager.Infrastructure.Files;

namespace ServerManager.Application.Tests.Files;

public class RemoteFileCommandsTests
{
    [Fact]
    public void Recursive_chown_never_follows_symbolic_links()
    {
        Assert.StartsWith("chown -R -P -- ", RemoteFileCommands.ChangeOwner("/srv/app", "deploy", null, recursive: true));
        Assert.StartsWith("chown -- ", RemoteFileCommands.ChangeOwner("/srv/app", "deploy", null, recursive: false));
    }

    [Fact]
    public void Resolution_uses_the_real_parent_for_the_entry()
    {
        var resolution = RemoteFileCommands.ParseResolution("SM_LINK=1\nSM_TARGET=/etc\nSM_PARENT=/srv/real\n", "/home/deploy/sys/root");

        Assert.NotNull(resolution);
        Assert.True(resolution.IsSymbolicLink);
        Assert.Equal("/etc", resolution.Target);
        Assert.Equal("/srv/real/root", resolution.Entry);
    }

    [Theory]
    [InlineData("SM_LINK=0\nSM_TARGET=/srv/a\n")]
    [InlineData("SM_LINK=0\nSM_TARGET=srv/a\nSM_PARENT=/srv\n")]
    [InlineData("SM_LINK=0\nSM_TARGET=/srv/../etc\nSM_PARENT=/srv\n")]
    [InlineData("SM_LINK=x\nSM_TARGET=/srv/a\nSM_PARENT=/srv\n")]
    public void Incomplete_or_unusual_resolution_is_rejected(string stdout) =>
        Assert.Null(RemoteFileCommands.ParseResolution(stdout, "/srv/a"));

    [Fact]
    public async Task Resolve_script_follows_links_on_a_real_shell()
    {
        if (OperatingSystem.IsWindows())
            return;

        var directory = Directory.CreateTempSubdirectory("sm-resolve-test-");
        try
        {
            var real = Directory.CreateDirectory(Path.Combine(directory.FullName, "real"));
            var link = Path.Combine(directory.FullName, "link");
            File.CreateSymbolicLink(link, real.FullName);

            var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(RemoteFileCommands.Resolve(link));
            using var process = Process.Start(start)!;
            var stdout = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);

            var resolution = RemoteFileCommands.ParseResolution(stdout, link);
            Assert.NotNull(resolution);
            Assert.True(resolution.IsSymbolicLink);
            Assert.EndsWith("/real", resolution.Target, StringComparison.Ordinal);
            Assert.EndsWith("/link", resolution.Entry, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

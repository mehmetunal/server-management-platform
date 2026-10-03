using ServerManager.Infrastructure.Dokploy;

namespace ServerManager.Application.Tests.Dokploy;

public class DokployCommandsTests
{
    [Fact]
    public void Run_script_quotes_version_and_path()
    {
        Assert.Equal("env DOKPLOY_VERSION='v0.25.3' bash '/tmp/x.sh'", DokployCommands.RunScript("/tmp/x.sh", "v0.25.3", useBash: true));
        Assert.Equal("sh '/tmp/x.sh'", DokployCommands.RunScript("/tmp/x.sh", null, useBash: false));
    }

    [Fact]
    public void Download_and_cleanup_quote_untrusted_values()
    {
        var download = DokployCommands.Download("https://example.com/install.sh?a='b'", "/tmp/x.sh");

        Assert.StartsWith("curl -fsSL --max-time 120 -o '/tmp/x.sh' ", download);
        Assert.EndsWith("'https://example.com/install.sh?a='\"'\"'b'\"'\"''", download);
        Assert.Equal("rm -f '/tmp/x.sh'", DokployCommands.Remove("/tmp/x.sh"));
    }

    [Fact]
    public void Reachability_only_fails_on_http_errors_when_requested()
    {
        Assert.Contains("curl -sSfL ", DokployCommands.Reachability("https://dokploy.com/install.sh", failOnHttpError: true));
        Assert.Contains("curl -sSL ", DokployCommands.Reachability("https://registry-1.docker.io/v2/", failOnHttpError: false));
    }

    [Fact]
    public void Local_health_targets_loopback_port()
    {
        var command = DokployCommands.LocalHealth(3000);

        Assert.StartsWith("sh -c ", command);
        Assert.Contains("http://127.0.0.1:3000/api/health", command);
    }
}

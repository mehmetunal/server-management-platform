using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Infrastructure.Docker;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Application.Tests.Docker;

public class DockerCommandsTests
{
    [Theory]
    [InlineData("web", "'web'")]
    [InlineData("it's", "'it'\"'\"'s'")]
    [InlineData("$(id); `id`", "'$(id); `id`'")]
    [InlineData("", "''")]
    public void Shell_quote_wraps_values_in_single_quotes(string value, string expected)
    {
        Assert.Equal(expected, ShellQuote.Quote(value));
    }

    [Theory]
    [InlineData(DockerContainerAction.Start, false, "docker start 'web'")]
    [InlineData(DockerContainerAction.Stop, false, "docker stop 'web'")]
    [InlineData(DockerContainerAction.Restart, false, "docker restart 'web'")]
    [InlineData(DockerContainerAction.Pause, false, "docker pause 'web'")]
    [InlineData(DockerContainerAction.Unpause, false, "docker unpause 'web'")]
    [InlineData(DockerContainerAction.Kill, false, "docker kill 'web'")]
    [InlineData(DockerContainerAction.Remove, false, "docker rm 'web'")]
    [InlineData(DockerContainerAction.Remove, true, "docker rm -f 'web'")]
    [InlineData(DockerContainerAction.Stop, true, "docker stop 'web'")]
    public void Builds_container_action_commands(DockerContainerAction action, bool force, string expected)
    {
        Assert.Equal(expected, DockerCommands.ContainerAction("web", action, force));
    }

    [Fact]
    public void Unknown_container_action_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DockerCommands.ContainerAction("web", (DockerContainerAction)99, false));
    }

    [Fact]
    public void Logs_command_quotes_since_and_container()
    {
        Assert.Equal("docker logs --timestamps --tail 200 'web'", DockerCommands.Logs("web", 200, null));
        Assert.Equal(
            "docker logs --timestamps --tail 50 --since '2026-10-03T18:48:19.219917720Z' 'web'",
            DockerCommands.Logs("web", 50, "2026-10-03T18:48:19.219917720Z"));
    }

    [Fact]
    public void Builds_image_volume_and_network_commands()
    {
        Assert.Equal("docker pull -q 'nginx:alpine'", DockerCommands.Pull("nginx:alpine"));
        Assert.Equal("docker image rm 'nginx:alpine'", DockerCommands.RemoveImage("nginx:alpine", false));
        Assert.Equal("docker image rm -f 'nginx:alpine'", DockerCommands.RemoveImage("nginx:alpine", true));
        Assert.Equal("docker image prune -f", DockerCommands.PruneImages(false));
        Assert.Equal("docker image prune -f -a", DockerCommands.PruneImages(true));
        Assert.Equal("docker volume create 'data'", DockerCommands.CreateVolume("data"));
        Assert.Equal("docker volume rm 'data'", DockerCommands.RemoveVolume("data"));
        Assert.Equal("docker network rm 'appnet'", DockerCommands.RemoveNetwork("appnet"));
        Assert.Equal("docker rename 'old' 'new'", DockerCommands.Rename("old", "new"));
    }

    [Fact]
    public void Create_network_includes_only_provided_options()
    {
        Assert.Equal(
            "docker network create --driver 'bridge' 'appnet'",
            DockerCommands.CreateNetwork(new CreateNetworkDto { Name = "appnet" }));

        Assert.Equal(
            "docker network create --driver 'bridge' --subnet '172.30.0.0/16' --gateway '172.30.0.1' --internal 'appnet'",
            DockerCommands.CreateNetwork(new CreateNetworkDto { Name = "appnet", Subnet = "172.30.0.0/16", Gateway = "172.30.0.1", Internal = true }));
    }

    [Fact]
    public void Stats_and_inspect_quote_every_name()
    {
        Assert.Equal("docker stats --no-stream --no-trunc --format '{{json .}}'", DockerCommands.Stats(null));
        Assert.EndsWith(" 'web'", DockerCommands.Stats("web"));
        Assert.Equal("docker container inspect 'a' 'b'", DockerCommands.InspectContainers(["a", "b"]));
        Assert.Equal("docker network inspect 'n1'", DockerCommands.InspectNetworks(["n1"]));
    }

    [Fact]
    public void Exec_shell_prefers_bash_and_falls_back_to_sh()
    {
        var command = DockerCommands.ExecShell("web");

        Assert.StartsWith("docker exec -it -e TERM=xterm-256color 'web' sh -c '", command);
        Assert.Contains("exec bash", command);
        Assert.Contains("exec sh", command);
    }
}

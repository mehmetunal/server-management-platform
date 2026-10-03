using ServerManager.Infrastructure.Dokploy;

namespace ServerManager.Application.Tests.Dokploy;

public class DokployOutputParserTests
{
    [Fact]
    public void Parses_key_values_and_collects_listen_lines()
    {
        const string output = """
            os_id=ubuntu
            os_name=Ubuntu 24.04.1 LTS
            uid=0
            ports_tool=ss
            listen=0.0.0.0:22
            listen=[::]:80
            os_id=ignored
            garbage line
            """;

        var (values, listening) = DokployOutputParser.ParseKeyValues(output);

        Assert.Equal("ubuntu", values["os_id"]);
        Assert.Equal("Ubuntu 24.04.1 LTS", values["os_name"]);
        Assert.Equal("0", values["uid"]);
        Assert.Equal(["0.0.0.0:22", "[::]:80"], listening);
    }

    [Fact]
    public void Extracts_unique_ports_from_listen_addresses()
    {
        var ports = DokployOutputParser.ParseListeningPorts(["0.0.0.0:80", "[::]:80", ":::3000", "*:443", "127.0.0.53%lo:53", "*:*", "0.0.0.0:", "bad"]);

        Assert.Equal([53, 80, 443, 3000], ports);
    }

    [Fact]
    public void Reads_docker_version_and_swarm_state()
    {
        const string json = """{"ServerVersion":"28.5.0","Swarm":{"LocalNodeState":"active","NodeID":"x"}}""";

        var (version, swarm) = DokployOutputParser.ParseDockerInfo(json);

        Assert.Equal("28.5.0", version);
        Assert.Equal("active", swarm);
    }

    [Fact]
    public void Invalid_docker_info_returns_nulls()
    {
        Assert.Equal((null, null), DokployOutputParser.ParseDockerInfo("permission denied"));
    }

    [Fact]
    public void Keeps_only_dokploy_services()
    {
        const string output = """
            {"ID":"a","Image":"dokploy/dokploy:v0.25.3","Mode":"replicated","Name":"dokploy","Ports":"*:3000->3000/tcp","Replicas":"1/1"}
            {"ID":"b","Image":"postgres:16","Mode":"replicated","Name":"dokploy-postgres","Ports":"","Replicas":"0/1 (max 1 per node)"}
            {"ID":"c","Image":"nginx","Mode":"replicated","Name":"customer-app","Ports":"","Replicas":"2/2"}
            """;

        var services = DokployOutputParser.ParseServices(output);

        Assert.Equal(["dokploy", "dokploy-postgres"], services.Select(s => s.Name));
        Assert.True(services[0].IsRunning);
        Assert.Equal("*:3000->3000/tcp", services[0].Ports);
        Assert.False(services[1].IsRunning);
        Assert.Null(services[1].Ports);
    }

    [Fact]
    public void Keeps_traefik_and_task_containers()
    {
        const string output = """
            {"Names":"dokploy-traefik","Image":"traefik:v3.5.0","State":"running","Status":"Up 2 hours"}
            {"Names":"dokploy.1.abc","Image":"dokploy/dokploy:latest","State":"running","Status":"Up 2 hours"}
            {"Names":"dokployer","Image":"x","State":"running","Status":"Up"}
            """;

        var containers = DokployOutputParser.ParseContainers(output);

        Assert.Equal(["dokploy-traefik", "dokploy.1.abc"], containers.Select(c => c.Name));
        Assert.All(containers, c => Assert.True(c.IsRunning));
    }

    [Theory]
    [InlineData("1/1", 1, 1)]
    [InlineData("0/1 (max 1 per node)", 0, 1)]
    [InlineData("", 0, 0)]
    [InlineData("global", 0, 0)]
    public void Parses_replicas(string replicas, int running, int desired)
    {
        Assert.Equal((running, desired), DokployOutputParser.ParseReplicas(replicas));
    }

    [Fact]
    public void Parses_sha256sum_output()
    {
        const string hash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

        Assert.Equal(hash.ToLowerInvariant(), DokployOutputParser.ParseSha256($"{hash}  /tmp/sm-dokploy-install.sh\n"));
        Assert.Null(DokployOutputParser.ParseSha256("sha256sum: missing"));
    }

    [Theory]
    [InlineData("""{"ok":true}""", true)]
    [InlineData("""{"ok": true}""", true)]
    [InlineData("""{"ok":false}""", false)]
    [InlineData("<html>", false)]
    public void Recognises_health_response(string body, bool healthy)
    {
        Assert.Equal(healthy, DokployOutputParser.IsHealthyResponse(body));
    }

    [Fact]
    public void Detects_inactive_swarm_error()
    {
        Assert.True(DokployOutputParser.IsSwarmInactive("Error response from daemon: This node is not a swarm manager. Use \"docker swarm init\"…"));
        Assert.False(DokployOutputParser.IsSwarmInactive("permission denied while trying to connect to the Docker daemon socket"));
    }
}

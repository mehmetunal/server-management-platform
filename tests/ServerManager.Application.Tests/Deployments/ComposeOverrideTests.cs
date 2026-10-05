using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Deployments;

public class ComposeOverrideTests
{
    private const string EnvPath = "/srv/apps/api/.env";

    private static DeploymentRoute Route(string service, string host = "api.ornek.com") => new()
    {
        RouterName = DomainNames.RouterName("api", host, string.Empty),
        Host = host,
        ContainerPort = 8080,
        ServiceName = service,
        TlsMode = DeploymentTlsMode.Cloudflare
    };

    [Fact]
    public void Env_file_is_added_to_every_service()
    {
        var yaml = ComposeOverride.Build(["web", "worker", "db"], EnvPath, []);

        Assert.Equal(
            "services:\n" +
            "  web:\n    env_file:\n      - \"/srv/apps/api/.env\"\n" +
            "  worker:\n    env_file:\n      - \"/srv/apps/api/.env\"\n" +
            "  db:\n    env_file:\n      - \"/srv/apps/api/.env\"\n",
            yaml);
        Assert.DoesNotContain("networks:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Env_file_and_routes_are_combined_and_default_network_is_kept()
    {
        var yaml = ComposeOverride.Build(["web", "db"], EnvPath, [Route("web")]);

        Assert.StartsWith(
            "services:\n  web:\n    env_file:\n      - \"/srv/apps/api/.env\"\n    networks:\n      - default\n      - " + DomainNames.ProxyNetwork + "\n    labels:\n",
            yaml,
            StringComparison.Ordinal);
        Assert.Contains("  db:\n    env_file:\n      - \"/srv/apps/api/.env\"\n", yaml, StringComparison.Ordinal);
        Assert.EndsWith("networks:\n  " + DomainNames.ProxyNetwork + ":\n    external: true\n", yaml, StringComparison.Ordinal);
        Assert.Equal(1, yaml.Split("traefik.enable=true").Length - 1);
    }

    [Fact]
    public void Routed_service_missing_from_service_list_still_gets_labels()
    {
        var yaml = ComposeOverride.Build(["db"], EnvPath, [Route("web")]);

        Assert.Contains("  db:\n    env_file:", yaml, StringComparison.Ordinal);
        Assert.Contains("  web:\n    networks:\n      - default\n", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Routes_only_matches_the_traefik_override()
    {
        var routes = new[] { Route("web") };

        Assert.Equal(TraefikRoutes.ComposeOverride(routes), ComposeOverride.Build([], null, routes));
        Assert.DoesNotContain("env_file", ComposeOverride.Build([], null, routes), StringComparison.Ordinal);
    }

    [Fact]
    public void No_services_and_no_routes_is_still_valid_yaml() =>
        Assert.Equal("services: {}\n", ComposeOverride.Build([], EnvPath, []));

    [Fact]
    public void Invalid_service_names_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => ComposeOverride.Build(["web\n  evil:"], EnvPath, []));
        Assert.Throws<InvalidOperationException>(() => ComposeOverride.Build(["web"], null, []));
    }

    [Fact]
    public void Parse_services_skips_blank_and_invalid_lines()
    {
        var services = ComposeOverride.ParseServices("web\n\nworker\r\nWARN[0000] bir uyarı\ndb\nweb\n");

        Assert.Equal(["web", "worker", "db"], services);
    }

    [Fact]
    public void Linked_services_network_is_added_to_every_service_with_default_network()
    {
        var yaml = ComposeOverride.Build([], EnvPath, [], ["web", "worker"]);

        Assert.Equal(
            "services:\n" +
            "  web:\n    networks:\n      - default\n      - " + DomainNames.ServicesNetwork + "\n" +
            "  worker:\n    networks:\n      - default\n      - " + DomainNames.ServicesNetwork + "\n" +
            "networks:\n  " + DomainNames.ServicesNetwork + ":\n    external: true\n",
            yaml);
    }

    [Fact]
    public void Env_routes_and_services_network_are_combined()
    {
        var yaml = ComposeOverride.Build(["web", "db"], EnvPath, [Route("web")], ["web", "db"]);

        Assert.StartsWith(
            "services:\n  web:\n    env_file:\n      - \"/srv/apps/api/.env\"\n    networks:\n      - default\n      - " + DomainNames.ProxyNetwork +
            "\n      - " + DomainNames.ServicesNetwork + "\n    labels:\n",
            yaml,
            StringComparison.Ordinal);
        Assert.Contains("  db:\n    env_file:\n      - \"/srv/apps/api/.env\"\n    networks:\n      - default\n      - " + DomainNames.ServicesNetwork + "\n", yaml, StringComparison.Ordinal);
        Assert.EndsWith(
            "networks:\n  " + DomainNames.ProxyNetwork + ":\n    external: true\n  " + DomainNames.ServicesNetwork + ":\n    external: true\n",
            yaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Services_network_names_are_validated() =>
        Assert.Throws<InvalidOperationException>(() => ComposeOverride.Build([], null, [], ["web\n  evil:"]));

    [Fact]
    public void Override_is_needed_when_project_has_linked_services() =>
        Assert.True(ComposeOverride.IsNeeded(false, [], joinServicesNetwork: true));

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(false, 1, true)]
    [InlineData(false, 0, false)]
    public void Override_is_needed_for_environment_or_routes(bool hasEnvironment, int routeCount, bool expected)
    {
        var routes = Enumerable.Range(0, routeCount).Select(_ => Route("web")).ToList();

        Assert.Equal(expected, ComposeOverride.IsNeeded(hasEnvironment, routes));
    }
}

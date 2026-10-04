using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Deployments;

public class TraefikRoutesTests
{
    private const string Pem = "-----BEGIN CERTIFICATE-----\nABC\n-----END CERTIFICATE-----";
    private const string Key = "-----BEGIN PRIVATE KEY-----\nSECRET\n-----END PRIVATE KEY-----";

    private static DeploymentRoute Route(DeploymentTlsMode mode, string host = "api.ornek.com", string path = "", string? service = "web") => new()
    {
        RouterName = DomainNames.RouterName("api", host, path),
        Host = host,
        Path = path,
        ContainerPort = 8080,
        ServiceName = service,
        TlsMode = mode,
        CertificatePem = mode == DeploymentTlsMode.Custom ? Pem : null,
        PrivateKeyPem = mode == DeploymentTlsMode.Custom ? Key : null
    };

    [Fact]
    public void Cloudflare_route_listens_only_on_http()
    {
        var labels = TraefikRoutes.Labels(Route(DeploymentTlsMode.Cloudflare));

        Assert.Contains(labels, label => label.EndsWith(".entrypoints=web", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, label => label.Contains("certresolver", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, label => label.Contains("websecure", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Contains("server.port=8080", StringComparison.Ordinal));
    }

    [Fact]
    public void Lets_encrypt_route_requests_a_certificate_and_redirects_http()
    {
        var labels = TraefikRoutes.Labels(Route(DeploymentTlsMode.LetsEncrypt, path: "/api"));

        Assert.Contains(labels, label => label.Contains("certresolver=letsencrypt", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Contains("Host(`api.ornek.com`) && PathPrefix(`/api`)", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Contains("redirectscheme.scheme=https", StringComparison.Ordinal));
    }

    [Fact]
    public void Custom_route_enables_tls_without_requesting_a_certificate()
    {
        var labels = TraefikRoutes.Labels(Route(DeploymentTlsMode.Custom));

        Assert.Contains(labels, label => label.EndsWith(".tls=true", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, label => label.Contains("certresolver", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, label => label.Contains(Key, StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_override_groups_one_service_onto_the_external_network()
    {
        var yaml = TraefikRoutes.ComposeOverride([Route(DeploymentTlsMode.Cloudflare), Route(DeploymentTlsMode.LetsEncrypt, "app.ornek.com")]);

        Assert.Contains("services:\n  web:\n", yaml, StringComparison.Ordinal);
        Assert.Contains("external: true", yaml, StringComparison.Ordinal);
        Assert.Contains(DomainNames.ProxyNetwork, yaml, StringComparison.Ordinal);
        Assert.Equal(1, yaml.Split("traefik.enable=true").Length - 1);
    }

    [Fact]
    public void Certificate_input_carries_the_key_and_the_shell_command_does_not()
    {
        var route = Route(DeploymentTlsMode.Custom);
        var input = TraefikRoutes.CertificateInput("api", [route]);
        var command = ServerManager.Infrastructure.Deployments.DeploymentCommands.SyncProxyFiles("api");

        Assert.Contains(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Key)), input, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, command, StringComparison.Ordinal);
        Assert.DoesNotContain(Pem, command, StringComparison.Ordinal);
        Assert.EndsWith("END\n", input, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("API.Ornek.com", "api.ornek.com")]
    [InlineData("api.ornek.com.", "api.ornek.com")]
    public void Host_is_normalized(string raw, string expected)
    {
        Assert.True(DomainNames.TryNormalizeHost(raw, out var host, out _));
        Assert.Equal(expected, host);
    }

    [Fact]
    public void Same_host_and_path_always_get_the_same_router_name()
    {
        Assert.Equal(DomainNames.RouterName("api", "api.ornek.com", ""), DomainNames.RouterName("api", "api.ornek.com", ""));
        Assert.NotEqual(DomainNames.RouterName("api", "api.ornek.com", ""), DomainNames.RouterName("api", "api.ornek.com", "/v1"));
    }
}

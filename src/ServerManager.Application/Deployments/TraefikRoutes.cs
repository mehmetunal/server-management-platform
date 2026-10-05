using System.Text;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Deployments;

/// <summary>Dokploy'un yaptığı gibi Traefik etiketleri ve compose override üretir. Sertifika metni etikete yazılmaz.</summary>
public static class TraefikRoutes
{
    public static string Rule(DeploymentRoute route)
    {
        var host = "Host(`" + route.Host + "`)";
        return string.IsNullOrEmpty(route.Path) ? host : host + " && PathPrefix(`" + route.Path + "`)";
    }

    public static IReadOnlyList<string> Labels(DeploymentRoute route)
    {
        var id = route.RouterName;
        var rule = Rule(route);
        var labels = new List<string>
        {
            "traefik.enable=true",
            "traefik.docker.network=" + DomainNames.ProxyNetwork,
            $"traefik.http.routers.{id}.rule={rule}",
            $"traefik.http.routers.{id}.service={id}",
            $"traefik.http.services.{id}.loadbalancer.server.port={route.ContainerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        };

        if (route.TlsMode == DeploymentTlsMode.Cloudflare)
        {
            labels.Add($"traefik.http.routers.{id}.entrypoints=web");
            return labels;
        }

        labels.Add($"traefik.http.routers.{id}.entrypoints=websecure");
        labels.Add(route.TlsMode == DeploymentTlsMode.LetsEncrypt
            ? $"traefik.http.routers.{id}.tls.certresolver=letsencrypt"
            : $"traefik.http.routers.{id}.tls=true");

        var web = id + "-web";
        labels.Add($"traefik.http.routers.{web}.rule={rule}");
        labels.Add($"traefik.http.routers.{web}.entrypoints=web");
        labels.Add($"traefik.http.routers.{web}.middlewares={id}-https");
        labels.Add($"traefik.http.routers.{web}.service={id}");
        labels.Add($"traefik.http.middlewares.{id}-https.redirectscheme.scheme=https");
        labels.Add($"traefik.http.middlewares.{id}-https.redirectscheme.permanent=true");
        return labels;
    }

    /// <summary>Compose override'ında servis anahtarı olamayacak (boş veya geçersiz servis adlı) ilk rota; yoksa null.</summary>
    public static DeploymentRoute? FindRouteWithoutService(IReadOnlyList<DeploymentRoute> routes) =>
        routes.FirstOrDefault(route => !DomainNames.IsValidServiceName(route.ServiceName));

    /// <summary>
    /// Servisi sm-proxy ağına ekler. <c>networks</c> yazıldığında compose örtük <c>default</c> ağını eklemez; servis
    /// projedeki diğer servislere (veritabanı vb.) ulaşabilsin diye <c>default</c> da listelenir.
    /// </summary>
    /// <exception cref="InvalidOperationException">Rotalardan birinin geçerli servis adı yoksa.</exception>
    public static string ComposeOverride(IReadOnlyList<DeploymentRoute> routes)
    {
        var missing = FindRouteWithoutService(routes);
        if (missing is not null)
            throw new InvalidOperationException($"{missing.Host} için Compose servis adı eksik veya geçersiz.");

        var builder = new StringBuilder();
        builder.Append("services:\n");
        foreach (var group in routes.GroupBy(route => route.ServiceName!, StringComparer.Ordinal))
        {
            builder.Append("  ").Append(group.Key).Append(":\n");
            builder.Append("    networks:\n      - default\n      - ").Append(DomainNames.ProxyNetwork).Append('\n');
            builder.Append("    labels:\n");
            foreach (var label in group.SelectMany(Labels).Distinct(StringComparer.Ordinal))
                builder.Append("      - \"").Append(label.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)).Append("\"\n");
        }

        builder.Append("networks:\n  ").Append(DomainNames.ProxyNetwork).Append(":\n    external: true\n");
        return builder.ToString();
    }

    /// <summary>Özel sertifikaları base64 satırları olarak yazar. Komut metninde sertifika yoktur.</summary>
    public static string CertificateInput(string slug, IReadOnlyList<DeploymentRoute> routes)
    {
        var builder = new StringBuilder();
        foreach (var route in routes.Where(route => route.TlsMode == DeploymentTlsMode.Custom))
        {
            var stem = DomainNames.FileStem(slug, route.RouterName);
            Append(builder, stem + ".crt", route.CertificatePem ?? string.Empty);
            Append(builder, stem + ".key", route.PrivateKeyPem ?? string.Empty);
            var yaml = "tls:\n  certificates:\n    - certFile: /etc/traefik/dynamic/" + stem + ".crt\n      keyFile: /etc/traefik/dynamic/" + stem + ".key\n";
            Append(builder, stem + ".yml", yaml);
        }

        builder.Append("END\n");
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string name, string content)
    {
        builder.Append(name).Append('\n');
        builder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(content))).Append('\n');
    }
}

using System.Text;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Deployments;

/// <summary>
/// Panelin compose projesine eklediği <see cref="DomainNames.OverrideFileName"/> dosyası. İki iş yapar:
/// <list type="bullet">
/// <item>Proje ortam değişkenleri varsa compose dosyasındaki <b>tüm</b> servislere <c>env_file</c> olarak <c>.env</c> eklenir;
/// compose'un kendisi <c>.env</c>'yi yalnızca <c>${DEĞİŞKEN}</c> yerleştirmesi için okur, container'a aktarmaz.</item>
/// <item>Domain bağlı servisler sm-proxy ağına alınır ve Traefik etiketleri yazılır (bkz. <see cref="TraefikRoutes"/>).</item>
/// <item>Projeye bağlı yönetilen servis varsa tüm servisler <see cref="DomainNames.ServicesNetwork"/> ağına da katılır;
/// uygulama servise container adıyla (<c>sm-svc-&lt;slug&gt;</c>) bağlanır.</item>
/// </list>
/// Servisin kendi <c>environment:</c> tanımı <c>env_file</c>'dan önce gelir; kullanıcının açık değerleri ezilmez.
/// </summary>
public static class ComposeOverride
{
    public const string EnvironmentFileName = ".env";

    /// <summary>Override gerekli mi: ortam değişkeni (boş olsa bile kayıt), rota veya bağlı servis varsa.</summary>
    public static bool IsNeeded(bool hasEnvironment, IReadOnlyList<DeploymentRoute> routes, bool joinServicesNetwork = false) =>
        hasEnvironment || routes.Count > 0 || joinServicesNetwork;

    /// <param name="envServices">.env'nin aktarılacağı servisler (<c>docker compose config --services</c>); ortam yoksa boş.</param>
    /// <param name="envFilePath">.env'nin mutlak yolu; <paramref name="envServices"/> boşsa kullanılmaz.</param>
    /// <param name="networkServices">
    /// <see cref="DomainNames.ServicesNetwork"/> ağına katılacak servisler (projeye bağlı servis varsa compose'daki tüm servisler); null veya boşsa ağ eklenmez.
    /// </param>
    /// <exception cref="InvalidOperationException">Rota veya servis adı geçersizse.</exception>
    public static string Build(
        IReadOnlyList<string> envServices,
        string? envFilePath,
        IReadOnlyList<DeploymentRoute> routes,
        IReadOnlyList<string>? networkServices = null)
    {
        networkServices ??= [];
        var missing = TraefikRoutes.FindRouteWithoutService(routes);
        if (missing is not null)
            throw new InvalidOperationException($"{missing.Host} için Compose servis adı eksik veya geçersiz.");

        var invalid = envServices.Concat(networkServices).FirstOrDefault(service => !DomainNames.IsValidServiceName(service));
        if (invalid is not null)
            throw new InvalidOperationException($"Compose servis adı geçersiz: {invalid}");

        if (envServices.Count > 0 && string.IsNullOrEmpty(envFilePath))
            throw new InvalidOperationException(".env yolu belirtilmedi.");

        var routed = routes
            .GroupBy(route => route.ServiceName!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        // Önce compose'daki sıra, ardından yalnızca ağ listesinde veya rotada geçen servisler.
        var services = envServices.Distinct(StringComparer.Ordinal).ToList();
        services.AddRange(networkServices.Distinct(StringComparer.Ordinal).Where(name => !services.Contains(name, StringComparer.Ordinal)));
        services.AddRange(routed.Keys.Where(name => !services.Contains(name, StringComparer.Ordinal)));
        var withEnv = envServices.ToHashSet(StringComparer.Ordinal);
        var joined = networkServices.ToHashSet(StringComparer.Ordinal);

        var builder = new StringBuilder();
        if (services.Count == 0)
        {
            builder.Append("services: {}\n");
            return builder.ToString();
        }

        builder.Append("services:\n");
        foreach (var service in services)
        {
            builder.Append("  ").Append(service).Append(":\n");
            if (withEnv.Contains(service))
                builder.Append("    env_file:\n      - ").Append(Quote(envFilePath!)).Append('\n');

            var isRouted = routed.TryGetValue(service, out var serviceRoutes);
            var isJoined = joined.Contains(service);
            if (!isRouted && !isJoined)
                continue;

            // networks yazıldığında compose örtük default ağını eklemez; servis projedeki diğer servislere ulaşabilsin diye default da listelenir.
            builder.Append("    networks:\n      - default\n");
            if (isRouted)
                builder.Append("      - ").Append(DomainNames.ProxyNetwork).Append('\n');
            if (isJoined)
                builder.Append("      - ").Append(DomainNames.ServicesNetwork).Append('\n');

            if (!isRouted)
                continue;

            builder.Append("    labels:\n");
            foreach (var label in serviceRoutes!.SelectMany(TraefikRoutes.Labels).Distinct(StringComparer.Ordinal))
                builder.Append("      - ").Append(Quote(label)).Append('\n');
        }

        if (routed.Count > 0 || joined.Count > 0)
        {
            builder.Append("networks:\n");
            if (routed.Count > 0)
                builder.Append("  ").Append(DomainNames.ProxyNetwork).Append(":\n    external: true\n");
            if (joined.Count > 0)
                builder.Append("  ").Append(DomainNames.ServicesNetwork).Append(":\n    external: true\n");
        }

        return builder.ToString();
    }

    /// <summary><c>docker compose config --services</c> çıktısındaki servis adları; geçersiz satırlar atlanır.</summary>
    public static IReadOnlyList<string> ParseServices(string? output) =>
        (output ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(DomainNames.IsValidServiceName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

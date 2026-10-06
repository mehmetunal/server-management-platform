using System.Globalization;
using ServerManager.Application.ManagedServices;

namespace ServerManager.Plugin.Services.Extra;

/// <summary>
/// Kanca örneği: kullanıcı <c>KC_HOSTNAME</c> verirse üretim kipi (<c>start</c>), vermezse geliştirme kipi (<c>start-dev</c>).
/// Argümanlar çekirdek tarafından kabuk kaçışıyla eklenir; kanca sunucuda komut çalıştıramaz.
/// </summary>
public sealed class KeycloakTemplateHooks : IServiceTemplateHooks
{
    public string TemplateKey => ExtraServicesPlugin.KeycloakKey;

    public IEnumerable<ServiceTemplateHookError> Validate(ServiceTemplateFormContext context)
    {
        if (!context.Environment.TryGetValue(KeycloakTemplateProvider.HostnameVariable, out var hostname))
            yield break;

        if (!IsValidHostname(hostname))
        {
            yield return new ServiceTemplateHookError("Environment",
                "KC_HOSTNAME tam bir adres (https://sso.example.com) veya alan adı (sso.example.com) olmalı.");
        }
    }

    public IReadOnlyList<string> BuildExtraArgs(ServiceTemplateCommandContext context) =>
        IsProduction(context.Environment) ? ["start"] : ["start-dev"];

    public async Task AfterInstallAsync(ServiceTemplateInstallContext context, IServiceTemplateHookLog log, CancellationToken cancellationToken)
    {
        var published = context.PublishedPorts.FirstOrDefault(p => p.ContainerPort == KeycloakTemplateProvider.HttpPort);
        var where = published is null
            ? $"yalnızca Docker ağında: http://{context.ContainerName}:{KeycloakTemplateProvider.HttpPort.ToString(CultureInfo.InvariantCulture)}/admin"
            : $"sunucu portu {published.HostPort.ToString(CultureInfo.InvariantCulture)} → /admin";
        await log.InfoAsync($"Keycloak yönetim konsolu {where}. Kullanıcı: {context.Credentials.Username}", cancellationToken);
        await log.InfoAsync("İlk girişten sonra kalıcı bir yönetici hesabı oluşturun; bootstrap hesabı geçicidir.", cancellationToken);
    }

    private static bool IsProduction(IReadOnlyDictionary<string, string> environment) =>
        environment.TryGetValue(KeycloakTemplateProvider.HostnameVariable, out var hostname) && !string.IsNullOrWhiteSpace(hostname);

    private static bool IsValidHostname(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host);

        return Uri.CheckHostName(value) is UriHostNameType.Dns or UriHostNameType.IPv4;
    }
}

using ServerManager.Application.ManagedServices;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Services.Extra;

/// <summary>
/// Meilisearch ve ClickHouse şablonları kod gerektirmez: <c>templates/*.json</c> dosyalarından çekirdek tarafından okunur.
/// Keycloak şablonu C# sağlayıcısı ve kancalarla tanımlanır.
/// </summary>
public sealed class ExtraServicesStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IServiceTemplateProvider, KeycloakTemplateProvider>();
        services.AddSingleton<IServiceTemplateHooks, KeycloakTemplateHooks>();
    }
}

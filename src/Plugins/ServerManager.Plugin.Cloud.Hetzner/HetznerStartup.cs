using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Cloud.Hetzner;

public sealed class HetznerStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HetznerOptions>(configuration.GetSection(HetznerOptions.SectionName));

        // API anahtarı Authorization başlığında olduğundan başka adrese yönlendirme izlenmez.
        services.AddHttpClient(HetznerPlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<ICloudProvider, HetznerCloudProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

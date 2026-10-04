using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Cloud.Scaleway;

public sealed class ScalewayStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScalewayOptions>(configuration.GetSection(ScalewayOptions.SectionName));

        // Gizli anahtar X-Auth-Token başlığında olduğundan başka adrese yönlendirme izlenmez.
        services.AddHttpClient(ScalewayPlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<ICloudProvider, ScalewayCloudProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

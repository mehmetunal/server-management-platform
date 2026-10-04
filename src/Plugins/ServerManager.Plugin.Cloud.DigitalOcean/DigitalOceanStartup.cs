using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Cloud.DigitalOcean;

public sealed class DigitalOceanStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DigitalOceanOptions>(configuration.GetSection(DigitalOceanOptions.SectionName));

        // API anahtarı Authorization başlığında olduğundan başka adrese yönlendirme izlenmez.
        services.AddHttpClient(DigitalOceanPlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<ICloudProvider, DigitalOceanCloudProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

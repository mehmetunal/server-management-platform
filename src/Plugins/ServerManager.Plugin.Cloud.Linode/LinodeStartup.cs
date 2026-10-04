using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Cloud.Linode;

public sealed class LinodeStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LinodeOptions>(configuration.GetSection(LinodeOptions.SectionName));

        // API anahtarı Authorization başlığında olduğundan başka adrese yönlendirme izlenmez.
        services.AddHttpClient(LinodePlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<ICloudProvider, LinodeCloudProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Cloud.Vultr;

public sealed class VultrStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VultrOptions>(configuration.GetSection(VultrOptions.SectionName));

        // API anahtarı Authorization başlığında olduğundan başka adrese yönlendirme izlenmez.
        services.AddHttpClient(VultrPlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<ICloudProvider, VultrCloudProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Notifications.Discord;

public sealed class DiscordStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DiscordOptions>(configuration.GetSection(DiscordOptions.SectionName));

        // Webhook adresi gizli bilgidir; izin listesi dışındaki bir adrese yönlendirilmesin diye yönlendirme izlenmez.
        services.AddHttpClient(DiscordPlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<INotificationChannelProvider, DiscordNotificationProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

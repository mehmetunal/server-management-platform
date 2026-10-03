using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Notifications.Telegram;

public sealed class TelegramStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));

        // Bot anahtarı adresin içinde olduğundan yönlendirmeler izlenmez.
        services.AddHttpClient(TelegramPlugin.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton<INotificationChannelProvider, TelegramNotificationProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

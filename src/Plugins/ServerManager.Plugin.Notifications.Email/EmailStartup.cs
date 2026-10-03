using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Notifications.Email;

public sealed class EmailStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<INotificationChannelProvider, EmailNotificationProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

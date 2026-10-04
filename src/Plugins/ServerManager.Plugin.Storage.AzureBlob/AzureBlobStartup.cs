using ServerManager.Application.Interfaces.Backups;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Storage.AzureBlob;

public sealed class AzureBlobStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBackupStorageProvider, AzureBlobBackupStorageProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

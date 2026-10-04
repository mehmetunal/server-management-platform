using ServerManager.Application.Interfaces.Backups;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Plugin.Storage.S3;

public sealed class S3Startup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBackupStorageProvider, S3BackupStorageProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

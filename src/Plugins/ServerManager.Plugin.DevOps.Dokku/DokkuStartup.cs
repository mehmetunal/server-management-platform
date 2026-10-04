using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Plugin.DevOps.Dokku.Core;
using ServerManager.Plugin.DevOps.Dokku.Installation;
using ServerManager.Plugin.DevOps.Dokku.Integration;
using ServerManager.Plugin.DevOps.Dokku.Services;
using ServerManager.Web.Framework.Plugins;
using ServerManager.Web.Framework.RateLimiting;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Plugin.DevOps.Dokku;

public sealed class DokkuStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DokkuOptions>(configuration.GetSection(DokkuOptions.SectionName));

        services.AddSingleton<IPermissionProvider, DokkuPermissionProvider>();
        services.AddSingleton<IAuditActionProvider, DokkuAuditActionProvider>();
        services.AddSingleton<IServerTabProvider, DokkuServerTabProvider>();

        services.AddSingleton<IDokkuProvider, SshDokkuProvider>();
        services.AddSingleton<DokkuInstallationManager>();
        services.AddScoped<IDokkuService, DokkuService>();

        services.Configure<RateLimiterOptions>(options => options.AddPerUserPolicy(DokkuPlugin.RateLimitPolicy, 20));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

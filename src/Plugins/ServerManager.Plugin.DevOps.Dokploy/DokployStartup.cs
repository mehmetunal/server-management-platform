using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Plugin.DevOps.Dokploy.BackgroundJobs;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.Data;
using ServerManager.Plugin.DevOps.Dokploy.Hubs;
using ServerManager.Plugin.DevOps.Dokploy.Installation;
using ServerManager.Plugin.DevOps.Dokploy.Integration;
using ServerManager.Plugin.DevOps.Dokploy.Services;
using ServerManager.Web.Framework.Plugins;
using ServerManager.Web.Framework.RateLimiting;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Plugin.DevOps.Dokploy;

public sealed class DokployStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DokployOptions>(configuration.GetSection(DokployOptions.SectionName));
        services.AddValidatorsFromAssembly(typeof(DokployStartup).Assembly, includeInternalTypes: true);

        services.AddSingleton<IPermissionProvider, DokployPermissionProvider>();
        services.AddSingleton<IAuditActionProvider, DokployAuditActionProvider>();
        services.AddSingleton<IServerTabProvider, DokployServerTabProvider>();

        services.AddSingleton<IDokployProvider, SshDokployProvider>();

        // API anahtarı başlığı başka bir adrese taşınmasın diye yönlendirmeler izlenmez.
        services.AddHttpClient(DokployApiClient.HttpClientName, (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<DokployOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.HttpTimeoutSeconds, 2, 120));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });
        services.AddSingleton<IDokployApiClient, DokployApiClient>();

        services.AddScoped<IDokployRepository, DokployRepository>();
        services.AddScoped<IDokployService, DokployService>();
        services.AddSingleton<DokployInstallationManager>();
        services.AddHostedService<DokployHealthWorker>();

        services.Configure<RateLimiterOptions>(options => options.AddPerUserPolicy(DokployPlugin.RateLimitPolicy, 20));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<DokployHub>(DokployHub.Path);
}

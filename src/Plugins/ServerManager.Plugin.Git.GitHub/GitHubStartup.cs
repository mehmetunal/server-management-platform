using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Plugin.Git.GitHub.Core;
using ServerManager.Plugin.Git.GitHub.Data;
using ServerManager.Plugin.Git.GitHub.Integration;
using ServerManager.Plugin.Git.GitHub.Services;
using ServerManager.Web.Framework.Navigation;
using ServerManager.Web.Framework.Plugins;
using ServerManager.Web.Framework.RateLimiting;

namespace ServerManager.Plugin.Git.GitHub;

public sealed class GitHubStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(configuration.GetSection(GitHubOptions.SectionName));
        services.AddValidatorsFromAssembly(typeof(GitHubStartup).Assembly, includeInternalTypes: true);
        services.AddMemoryCache();

        services.AddSingleton<IPermissionProvider, GitHubPermissionProvider>();
        services.AddSingleton<IAuditActionProvider, GitHubAuditActionProvider>();
        services.AddSingleton<IMenuItemProvider, GitHubMenuItemProvider>();

        // JWT ve kurulum anahtarı başka bir adrese taşınmasın diye yönlendirmeler izlenmez.
        services.AddHttpClient(GitHubApiClient.HttpClientName, (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<GitHubOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.HttpTimeoutSeconds, 2, 120));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });
        services.AddSingleton<IGitHubApiClient, GitHubApiClient>();
        services.AddSingleton<IGitHubAppGateway, GitHubAppGateway>();

        services.AddScoped<IGitHubAppRepository, GitHubAppRepository>();
        services.AddScoped<IGitHubAppService, GitHubAppService>();
        services.AddScoped<IGitIntegration, GitHubGitIntegration>();

        services.Configure<RateLimiterOptions>(options => options.AddPerUserPolicy(GitHubPlugin.RateLimitPolicy, 20));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

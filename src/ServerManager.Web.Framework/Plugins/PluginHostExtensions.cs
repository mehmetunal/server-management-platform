using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using ServerManager.Application.Plugins;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Framework.Plugins;

public static class PluginHostExtensions
{
    /// <summary>
    /// Eklentileri yükler: servislerini kaydeder, controller ve derlenmiş view'larını MVC'ye, <c>Content</c> klasörünü
    /// web köküne ekler. Etkinlik denetimi çalışma anında yapıldığı için eklenti etkinleştirmek yeniden başlatma gerektirmez.
    /// </summary>
    public static IPluginCatalog AddPlugins(this WebApplicationBuilder builder, IMvcBuilder mvcBuilder)
    {
        var options = builder.Configuration.GetSection(PluginOptions.SectionName).Get<PluginOptions>() ?? new PluginOptions();
        var directory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.Directory));
        var result = PluginLoader.Load(directory);

        builder.Services.AddSingleton<IPluginCatalog>(result.Catalog);
        builder.Services.AddSingleton(result);
        builder.Services.AddSingleton<ServerTabRegistry>();
        builder.Services.Configure<MvcOptions>(mvc => mvc.Filters.Add<PluginEnabledFilter>());
        builder.Services.Configure<HubOptions>(hub => hub.AddFilter<PluginHubFilter>());

        foreach (var startup in result.Startups)
            startup.ConfigureServices(builder.Services, builder.Configuration);

        foreach (var assembly in result.Catalog.LoadedAssemblies)
        {
            foreach (var part in ApplicationPartFactory.GetApplicationPartFactory(assembly).GetApplicationParts(assembly))
                mvcBuilder.PartManager.ApplicationParts.Add(part);
        }

        builder.Environment.WebRootFileProvider = new CompositeFileProvider(
            builder.Environment.WebRootFileProvider,
            new PluginContentFileProvider(result.Catalog));

        return result.Catalog;
    }

    public static void MapPluginEndpoints(this WebApplication app)
    {
        foreach (var startup in app.Services.GetRequiredService<PluginLoadResult>().Startups)
            startup.MapEndpoints(app);
    }
}

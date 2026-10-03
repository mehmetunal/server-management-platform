using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Web.Framework.Plugins;

namespace ServerManager.Application.Tests.Plugins;

/// <summary>Yükleyicinin eklenti assembly'sindeki giriş noktalarını bulduğunu doğrulamak için.</summary>
public sealed class TestPluginStartup : IPluginStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}

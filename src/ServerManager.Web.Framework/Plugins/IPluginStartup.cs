namespace ServerManager.Web.Framework.Plugins;

/// <summary>
/// Eklentinin giriş noktası. Eklenti etkin olsun olmasın açılışta çağrılır; devre dışı eklentinin controller, hub ve
/// sekmeleri çalışma anında engellenir, arka plan işleri <see cref="ServerManager.Application.Plugins.IPluginCatalog"/> ile durumu kontrol etmelidir.
/// </summary>
public interface IPluginStartup
{
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

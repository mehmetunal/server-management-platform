using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Plugins;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>
/// Eklentiler uygulamanın varsayılan AssemblyLoadContext'ine yüklenir; uygulamada daha eski sürümü bulunan ortak bir
/// bağımlılık (ör. Azure.Core) eklentinin yüklenmesini çalışma anında bozar. Paket güncellemelerinde bu test uyarır.
/// </summary>
[Collection(WebCollection.Name)]
public sealed class PluginLoadingTests(ServerManagerWebFactory factory)
{
    [Fact]
    public void All_bundled_plugins_load_without_error()
    {
        using var client = factory.CreateTestClient();
        var catalog = factory.Services.GetRequiredService<IPluginCatalog>();

        var failures = catalog.Plugins
            .Where(p => p.LoadError is not null)
            .Select(p => $"{p.Descriptor.SystemName}: {p.LoadError}")
            .ToList();

        Assert.NotEmpty(catalog.Plugins);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}

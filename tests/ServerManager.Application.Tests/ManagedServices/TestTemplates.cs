using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

/// <summary>Eklentisiz katalog: testlerde şablon aramaları da üretimdeki gibi <see cref="IServiceTemplateCatalog"/> üzerinden yapılır.</summary>
internal static class TestTemplates
{
    public static readonly ServiceTemplateCatalog Catalog = ServiceTemplateCatalog.BuiltInOnly();

    public static ServiceTemplate? Find(string? key) => Catalog.Find(key);

    public static IEnumerable<ServiceTemplate> ByCategory(ManagedServiceCategory category) =>
        Catalog.GetAvailable().Where(t => t.Category == category);
}

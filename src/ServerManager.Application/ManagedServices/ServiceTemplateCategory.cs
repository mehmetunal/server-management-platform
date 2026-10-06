using ServerManager.Domain.Enums;

namespace ServerManager.Application.ManagedServices;

/// <summary>Servis ekleme sayfasındaki şablon grubu.</summary>
/// <param name="Key">
/// Grup anahtarı. Yerleşik gruplar <c>database</c> ve <c>application</c>; eklenti grupları eklentinin şablon önekiyle
/// başlamalıdır (ör. <c>services.extra.search</c>).
/// </param>
/// <param name="DisplayName">Bölüm başlığı (ör. "Arama motorları").</param>
/// <param name="Order">Sayfadaki sıra; küçük olan önce gelir. Yerleşik gruplar 10 ve 20'dir.</param>
public sealed record ServiceTemplateCategory(string Key, string DisplayName, int Order = 100);

public static class ServiceTemplateCategories
{
    public const string Database = "database";
    public const string Application = "application";

    public static readonly IReadOnlyList<ServiceTemplateCategory> BuiltIn =
    [
        new(Database, "Veritabanları", 10),
        new(Application, "Uygulamalar", 20)
    ];

    public static string KeyOf(ManagedServiceCategory category) =>
        category == ManagedServiceCategory.Database ? Database : Application;
}

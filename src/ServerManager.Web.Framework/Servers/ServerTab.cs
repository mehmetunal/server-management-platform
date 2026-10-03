namespace ServerManager.Web.Framework.Servers;

/// <summary>Sunucu detay sayfasına eklenti tarafından eklenen sekme. Adres <c>/{Controller}/{Action}/{sunucuId}</c> olur.</summary>
public sealed record ServerTab(
    string Key,
    string Title,
    string Tooltip,
    string Lead,
    string Permission,
    string Controller,
    string Action = "Index",
    int Order = 100);

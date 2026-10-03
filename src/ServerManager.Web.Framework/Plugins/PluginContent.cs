namespace ServerManager.Web.Framework.Plugins;

/// <summary>Eklentinin <c>Content</c> klasörü <c>/plugins/{systemname}/</c> adresinden sunulur.</summary>
public static class PluginContent
{
    public const string FolderName = "Content";
    public const string RootSegment = "plugins";

    /// <summary>View'da <c>ViewData[PagePluginKey] = SystemName</c> atanınca sayfa CSS/JS dosyaları eklentinin klasöründen yüklenir.</summary>
    public const string PagePluginKey = "PagePlugin";

    public static string BasePath(string systemName) =>
        $"{RootSegment}/{systemName.ToLowerInvariant()}";
}

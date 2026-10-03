namespace ServerManager.Web.Middleware;

public static class FramingExtensions
{
    private const string ItemKey = "SameOriginFramingAllowed";

    /// <summary>
    /// Bu yanıtın yalnızca aynı kaynaktan çerçeve içinde açılmasına izin verir
    /// (gizli iframe ile yapılan indirmenin hata metnini okumak için).
    /// </summary>
    public static void AllowSameOriginFraming(this HttpContext context) =>
        context.Items[ItemKey] = true;

    public static bool IsSameOriginFramingAllowed(this HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is true;
}

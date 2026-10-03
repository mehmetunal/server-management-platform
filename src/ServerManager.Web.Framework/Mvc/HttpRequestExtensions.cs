namespace ServerManager.Web.Framework.Mvc;

public static class HttpRequestExtensions
{
    public static bool WantsJson(this HttpRequest request) =>
        request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        || request.IsAjax();

    /// <summary>Sayfa içinden fetch ile yapılan istek; liste action'ları bu durumda yalnızca partial döner.</summary>
    public static bool IsAjax(this HttpRequest request) =>
        string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}

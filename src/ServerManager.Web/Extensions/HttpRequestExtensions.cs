namespace ServerManager.Web.Extensions;

public static class HttpRequestExtensions
{
    public static bool WantsJson(this HttpRequest request) =>
        request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        || string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}

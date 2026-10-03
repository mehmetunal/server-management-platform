namespace ServerManager.Web.Middleware;

public static class CspNonceExtensions
{
    internal const string ItemKey = "CspNonce";

    public static string GetCspNonce(this HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var nonce) && nonce is string value ? value : string.Empty;
}

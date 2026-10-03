using Microsoft.AspNetCore.Http;

namespace ServerManager.Web.Framework.Security;

public static class ContentSecurityPolicyExtensions
{
    private const string FormActionItemKey = "CspFormActionOrigins";

    /// <summary>
    /// Bu yanıttaki formların verilen kaynağa (yalnızca şema + host, ör. https://github.com) gönderilmesine izin verir.
    /// Geçersiz veya http/https dışı adresler yok sayılır.
    /// </summary>
    public static void AllowFormAction(this HttpContext context, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return;

        var origins = context.Items[FormActionItemKey] as HashSet<string> ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        origins.Add(uri.GetLeftPart(UriPartial.Authority));
        context.Items[FormActionItemKey] = origins;
    }

    public static IReadOnlyCollection<string> GetFormActionOrigins(this HttpContext context) =>
        context.Items[FormActionItemKey] as HashSet<string> ?? (IReadOnlyCollection<string>)[];
}

namespace ServerManager.Application.Dokploy;

public static class DokployUrls
{
    public const int MaxLength = 500;

    public static string BuildDefault(string host, int port)
    {
        var formattedHost = host.Contains(':') && !host.StartsWith('[') ? $"[{host}]" : host;
        return $"http://{formattedHost}:{port}";
    }

    /// <summary>Yalnızca http/https, kullanıcı bilgisi, sorgu ve parça içermeyen mutlak adres kabul edilir.</summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
            return false;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return true;
    }

    public static int? GetPort(string? baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Port : null;
}

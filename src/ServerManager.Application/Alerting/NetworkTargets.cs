using System.Net;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Alerting;

/// <summary>Uptime ve SSL kontrollerinde girilen adreslerin biçim kontrolü (çözümleme ve IP kısıtları yoklama sırasında yapılır).</summary>
public static partial class NetworkTargets
{
    public const int MaxHostLength = 253;
    public const int MaxUrlLength = 500;

    public static bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > MaxHostLength)
            return false;

        var value = host.Trim();
        if (value.StartsWith('[') && value.EndsWith(']'))
            value = value[1..^1];

        return IPAddress.TryParse(value, out _) || HostnamePattern().IsMatch(value);
    }

    public static bool TryValidateUrl(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Adres zorunludur.";
            return false;
        }

        if (url.Length > MaxUrlLength)
        {
            error = $"Adres en fazla {MaxUrlLength} karakter olabilir.";
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            error = "http:// veya https:// ile başlayan geçerli bir adres girin.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Adrese kullanıcı adı veya parola yazılamaz.";
            return false;
        }

        if (!IsValidHost(uri.Host))
        {
            error = "Adresteki sunucu adı geçersiz.";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^(?=.{1,253}$)([A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)(\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*\.?$")]
    private static partial Regex HostnamePattern();
}

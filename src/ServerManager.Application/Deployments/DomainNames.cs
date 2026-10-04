using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>Panelin kurduğu Traefik ve domain adlarının kuralları.</summary>
public static partial class DomainNames
{
    public const string ProxyContainer = "sm-traefik";
    public const string ProxyNetwork = "sm-proxy";
    public const string TraefikImage = "traefik:v3.5";
    public const string DynamicDirectory = "/var/lib/sm-traefik/dynamic";
    public const string CertificateDirectory = "/var/lib/sm-traefik/letsencrypt";
    public const string OverrideFileName = "sm-proxy.override.yml";
    public const int MaxDomainsPerProject = 20;
    public const int MaxCertificateLength = 100_000;

    public const string NotReadyMessage = "Henüz dağıtılmış bir uygulama yok. Yönlendirme ilk başarılı dağıtımdan sonra uygulanır.";
    public const string ProxyMissingMessage = "Sunucuda vekil çalışmıyor. Domain kartından vekili kurun.";

    public static bool TryNormalizeHost(string? value, out string host, out string? error)
    {
        host = (value ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length is 0 or > 253 || !HostPattern().IsMatch(host) || host.Contains("..", StringComparison.Ordinal))
        {
            error = "Geçerli bir alan adı girin (ör. api.ornek.com).";
            host = string.Empty;
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryNormalizePath(string? value, out string path, out string? error)
    {
        path = (value ?? string.Empty).Trim();
        if (path.Length == 0 || path == "/")
        {
            path = string.Empty;
            error = null;
            return true;
        }

        if (path.Length > 200 || !path.StartsWith('/') || !PathPattern().IsMatch(path) || path.Contains("..", StringComparison.Ordinal))
        {
            error = "Yol / ile başlamalı ve yalnızca harf, rakam, nokta, alt çizgi, tire ve / içermelidir.";
            path = string.Empty;
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsValidServiceName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= 63 && ServicePattern().IsMatch(name);

    public static bool IsCertificatePem(string? value) =>
        value is not null && value.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal) && value.Contains("END CERTIFICATE", StringComparison.Ordinal);

    public static bool IsPrivateKeyPem(string? value) =>
        value is not null
        && value.Contains("PRIVATE KEY", StringComparison.Ordinal)
        && value.Contains("BEGIN", StringComparison.Ordinal)
        && value.Contains("END", StringComparison.Ordinal);

    public static bool IsAcmeEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 200 && EmailPattern().IsMatch(email);

    /// <summary>Traefik yönlendirici adı. Aynı host ve yol her zaman aynı adı üretir.</summary>
    public static string RouterName(string slug, string host, string path)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(host + "\n" + path)))[..8].ToLowerInvariant();
        var prefix = slug.Length > 20 ? slug[..20].TrimEnd('-') : slug;
        if (prefix.Length == 0)
            prefix = "app";
        return prefix + "-" + hash;
    }

    public static string FileStem(string slug, string routerName) => slug + "--" + routerName;

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)+$")]
    private static partial Regex HostPattern();

    [GeneratedRegex(@"^/[A-Za-z0-9._/-]+$")]
    private static partial Regex PathPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$")]
    private static partial Regex ServicePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

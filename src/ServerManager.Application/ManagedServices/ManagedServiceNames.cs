using System.Text.RegularExpressions;
using ServerManager.Application.Deployments;

namespace ServerManager.Application.ManagedServices;

/// <summary>Servis kısa adından (slug) türetilen Docker ve dosya sistemi adları.</summary>
public static partial class ManagedServiceNames
{
    public const int MaxSlugLength = 40;
    public const string Prefix = "sm-svc-";

    /// <summary>Tüm servislerin katıldığı panel ağı; uygulamalar servise container adıyla buradan bağlanır.</summary>
    public const string ServicesNetwork = DomainNames.ServicesNetwork;

    /// <summary>Panelin Traefik vekil ağı (deployment domain'leri ile aynı ağ).</summary>
    public const string ProxyNetwork = DomainNames.ProxyNetwork;

    /// <summary>Servis başına ortam dosyası ve güvenlik duvarı betiğinin tutulduğu kök klasör.</summary>
    public const string StateRoot = "/var/lib/sm-services";

    /// <summary>Sunucu klasörü modunda panelin oluşturduğu klasöre yazılan işaret; veri silme yalnızca bu dosya varsa yapılır.</summary>
    public const string DataMarkerFile = ".sm-service";

    public static string Slugify(string? name)
    {
        // Ad harf/rakam içermiyorsa deployment kısa adının varsayılanı ("app") yerine "service" kullanılır.
        if (!(name ?? string.Empty).Any(char.IsLetterOrDigit))
            return "service";

        var slug = DeploymentNames.Slugify(name);
        if (slug.Length > MaxSlugLength)
            slug = slug[..MaxSlugLength].TrimEnd('-');

        return slug.Length == 0 ? "service" : slug;
    }

    public static string WithSuffix(string slug, int number)
    {
        var suffix = $"-{number}";
        var stem = slug.Length + suffix.Length > MaxSlugLength ? slug[..(MaxSlugLength - suffix.Length)].TrimEnd('-') : slug;
        return stem + suffix;
    }

    public static bool IsValidSlug(string? slug) => slug is not null && SlugPattern().IsMatch(slug);

    public static string ContainerName(string slug) => Prefix + slug;

    public static string VolumeName(string slug) => Prefix + slug + "-data";

    /// <summary>Güvenlik duvarı kurallarına eklenen yorum; kurallar bu etiketle listelenir ve silinir.</summary>
    public static string FirewallTag(string slug) => Prefix + slug;

    public static string StateDirectory(string slug) => $"{StateRoot}/{slug}";

    public static string EnvironmentFile(string slug) => $"{StateDirectory(slug)}/.env";

    public static string FirewallScript(string slug) => $"{StateDirectory(slug)}/firewall.sh";

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,38}[a-z0-9])?$")]
    private static partial Regex SlugPattern();
}

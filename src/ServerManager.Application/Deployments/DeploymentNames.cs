using System.Text;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>Proje kısa adı (slug) ve ondan türetilen Docker adları.</summary>
public static partial class DeploymentNames
{
    public const int MaxSlugLength = 48;
    private const string Prefix = "sm-";

    public static string Slugify(string? name)
    {
        var builder = new StringBuilder();
        foreach (var c in (name ?? string.Empty).Trim().ToLowerInvariant())
        {
            var mapped = c switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ı' => 'i',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                _ => c
            };

            if (mapped is >= 'a' and <= 'z' or >= '0' and <= '9')
                builder.Append(mapped);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > MaxSlugLength)
            slug = slug[..MaxSlugLength].TrimEnd('-');

        return slug.Length == 0 ? "app" : slug;
    }

    /// <summary>Çakışma durumunda "-2", "-3"… eki eklenir; toplam uzunluk sınırı korunur.</summary>
    public static string WithSuffix(string slug, int number)
    {
        var suffix = $"-{number}";
        var stem = slug.Length + suffix.Length > MaxSlugLength ? slug[..(MaxSlugLength - suffix.Length)].TrimEnd('-') : slug;
        return stem + suffix;
    }

    public static bool IsValidSlug(string? slug) => slug is not null && SlugPattern().IsMatch(slug);

    public static string ComposeProjectName(string slug) => Prefix + slug;

    public static string ContainerName(string slug) => Prefix + slug;

    public static string ImageName(string slug) => Prefix + slug;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,46}[a-z0-9])?$")]
    private static partial Regex SlugPattern();
}

using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>
/// Formda tek alanda taşınan entegrasyon + bağlantı kimliği: <c>{SystemName}|{sourceId}</c>.
/// SystemName yalnızca harf, rakam ve nokta içerdiği için ilk ayraç güvenle bölünür.
/// </summary>
public static partial class GitSourceKeys
{
    public const int MaxLength = 300;
    private const char Separator = '|';

    public static string Format(string systemName, string sourceId) => systemName + Separator + sourceId;

    public static bool TryParse(string? key, out string systemName, out string sourceId)
    {
        systemName = string.Empty;
        sourceId = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxLength)
            return false;

        var index = key.IndexOf(Separator, StringComparison.Ordinal);
        if (index <= 0 || index == key.Length - 1)
            return false;

        var name = key[..index];
        var id = key[(index + 1)..];
        if (!SystemNamePattern().IsMatch(name) || !SourceIdPattern().IsMatch(id))
            return false;

        systemName = name;
        sourceId = id;
        return true;
    }

    /// <summary>Entegrasyondaki depo adı: <c>sahip/depo</c> veya GitLab gibi alt grup içeren <c>grup/alt/depo</c>.</summary>
    public static bool IsValidRepository(string? repository) =>
        !string.IsNullOrEmpty(repository)
        && repository.Length <= 200
        && RepositoryPattern().IsMatch(repository)
        && !repository.Split('/').Any(segment => segment is "." or "..");

    [GeneratedRegex(@"^[A-Za-z0-9.]+$")]
    private static partial Regex SystemNamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9._:\-]{1,200}$")]
    private static partial Regex SourceIdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]+(/[A-Za-z0-9_.\-]+)+$")]
    private static partial Regex RepositoryPattern();
}

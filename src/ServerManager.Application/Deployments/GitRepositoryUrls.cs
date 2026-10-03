using System.Text.RegularExpressions;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Deployments;

/// <summary>
/// Depo adresi doğrulaması. Kimlik bilgisi adrese gömülemez (git config ve process listesinde görünür);
/// erişim anahtarı ayrı alanda şifreli saklanır ve yalnızca https adreslerinde kullanılır.
/// </summary>
public static partial class GitRepositoryUrls
{
    public const int MaxLength = 500;

    public static bool TryValidate(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Depo adresi zorunludur.";
            return false;
        }

        if (url.Length > MaxLength || !SafeCharacters().IsMatch(url))
        {
            error = "Depo adresi boşluk, tırnak veya ters bölü içeremez.";
            return false;
        }

        if (ScpLikePattern().IsMatch(url))
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            error = "Geçerli bir depo adresi girin (https://, ssh://, git@sunucu:yol veya file://).";
            return false;
        }

        switch (uri.Scheme)
        {
            case "https" or "http":
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    error = "Adrese kullanıcı adı veya anahtar yazmayın; erişim anahtarı alanını kullanın.";
                    return false;
                }

                if (uri.AbsolutePath.Trim('/').Length == 0)
                {
                    error = "Depo yolunu da yazın (ör. https://github.com/kurum/proje.git).";
                    return false;
                }

                return true;
            case "ssh":
                if (uri.UserInfo.Contains(':', StringComparison.Ordinal))
                {
                    error = "SSH adresine parola yazılamaz.";
                    return false;
                }

                return true;
            case "file":
                return uri.AbsolutePath.Length > 1;
            default:
                error = "Desteklenen adresler: https://, http://, ssh://, git@sunucu:yol ve file://.";
                return false;
        }
    }

    public static bool IsHttps(string? url) =>
        url is not null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// İki https adresi aynı sunucuya mı gidiyor. Kayıtlı erişim anahtarı yalnızca girildiği sunucuya gönderilir;
    /// adres başka bir sunucuya çevrilirse anahtar yeniden istenir.
    /// </summary>
    public static bool HaveSameHost(string? first, string? second) =>
        Uri.TryCreate(first, UriKind.Absolute, out var a)
        && Uri.TryCreate(second, UriKind.Absolute, out var b)
        && string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Authority, b.Authority, StringComparison.OrdinalIgnoreCase);

    /// <summary>Erişim anahtarıyla HTTPS kimlik doğrulamasında sağlayıcının beklediği kullanıcı adı.</summary>
    public static string TokenUsername(GitProvider provider, string? username) =>
        !string.IsNullOrWhiteSpace(username)
            ? username.Trim()
            : provider switch
            {
                GitProvider.GitHub => "x-access-token",
                GitProvider.GitLab => "oauth2",
                GitProvider.Bitbucket => "x-token-auth",
                _ => "git"
            };

    /// <summary>Commit'in web arayüzündeki adresi; adres https değilse veya tanınmıyorsa null.</summary>
    public static string? CommitUrl(GitProvider provider, string? url, string? sha)
    {
        if (!IsHttps(url) || string.IsNullOrEmpty(sha) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];

        var segment = provider switch
        {
            GitProvider.GitLab => "/-/commit/",
            GitProvider.Bitbucket => "/commits/",
            _ => "/commit/"
        };

        return $"{uri.Scheme}://{uri.Authority}{path}{segment}{sha}";
    }

    [GeneratedRegex(@"^[^\s'""`\\]+$")]
    private static partial Regex SafeCharacters();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+@[A-Za-z0-9.-]+:(?!/)[A-Za-z0-9._~/-]+$")]
    private static partial Regex ScpLikePattern();
}

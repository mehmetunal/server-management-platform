namespace ServerManager.Plugin.Git.GitHub.Core;

public static class GitHubUrls
{
    public static string InstallUrl(string webUrl, string slug) =>
        $"{webUrl.TrimEnd('/')}/apps/{Uri.EscapeDataString(slug)}/installations/new";

    /// <summary>Manifest formunun gönderileceği adres; kurum boşsa kullanıcının kendi hesabında oluşturulur.</summary>
    public static string ManifestPostUrl(string webUrl, string? organization, string state)
    {
        var root = webUrl.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(organization)
            ? "/settings/apps/new"
            : $"/organizations/{Uri.EscapeDataString(organization.Trim())}/settings/apps/new";
        return $"{root}{path}?state={Uri.EscapeDataString(state)}";
    }

    public static string RepositoryPath(string fullName) =>
        string.Join('/', fullName.Split('/').Select(Uri.EscapeDataString));

    /// <summary><c>owner/repo</c> biçimindeki adın depo kısmı (kurulum anahtarını depoya sınırlamak için).</summary>
    public static string RepositoryName(string fullName)
    {
        var separator = fullName.IndexOf('/');
        return separator < 0 ? fullName : fullName[(separator + 1)..];
    }
}

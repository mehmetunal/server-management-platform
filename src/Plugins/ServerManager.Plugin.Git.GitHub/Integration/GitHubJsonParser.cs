using System.Globalization;
using System.Text.Json;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Integration;

/// <summary>GitHub REST yanıtlarını okur; beklenmeyen biçimde null döner, hiçbir zaman istisna fırlatmaz.</summary>
public static class GitHubJsonParser
{
    public static GitHubAppInfo? ParseApp(string json) =>
        Parse(json, root => ReadApp(root));

    public static IReadOnlyList<GitHubInstallationInfo>? ParseInstallations(string json) =>
        Parse(json, root => root.ValueKind != JsonValueKind.Array
            ? null
            : root.EnumerateArray().Select(ReadInstallation).OfType<GitHubInstallationInfo>().ToList());

    public static GitHubInstallationToken? ParseToken(string json) =>
        Parse(json, root =>
        {
            var token = GetString(root, "token");
            var expiresAt = GetString(root, "expires_at");
            return string.IsNullOrEmpty(token)
                || !DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires)
                ? null
                : new GitHubInstallationToken(token, expires);
        });

    /// <summary><c>GET /installation/repositories</c> sayfası: depolar ve toplam sayı.</summary>
    public static GitHubRepositoryPage? ParseRepositoryPage(string json) =>
        Parse(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("repositories", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return null;

            var total = root.TryGetProperty("total_count", out var count) && count.TryGetInt32(out var value) ? value : 0;
            return new GitHubRepositoryPage(items.EnumerateArray().Select(ReadRepository).OfType<GitHubRepositoryInfo>().ToList(), total);
        });

    public static IReadOnlyList<string>? ParseBranches(string json) =>
        Parse(json, root => root.ValueKind != JsonValueKind.Array
            ? null
            : root.EnumerateArray()
                .Select(item => GetString(item, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList());

    public static GitHubManifestConversion? ParseConversion(string json) =>
        Parse(json, root =>
        {
            var app = ReadApp(root);
            var pem = GetString(root, "pem");
            return app is null || string.IsNullOrWhiteSpace(pem)
                ? null
                : new GitHubManifestConversion(
                    app.Id,
                    app.Slug,
                    app.Name,
                    GetString(root, "client_id"),
                    GetString(root, "client_secret"),
                    GetString(root, "webhook_secret"),
                    pem,
                    app.OwnerLogin,
                    app.HtmlUrl);
        });

    private static T? Parse<T>(string json, Func<JsonElement, T?> read)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            using var document = JsonDocument.Parse(json);
            return read(document.RootElement);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            return default;
        }
    }

    private static GitHubAppInfo? ReadApp(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var id) || !id.TryGetInt64(out var appId))
            return null;

        var slug = GetString(root, "slug");
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var owner = root.TryGetProperty("owner", out var ownerElement) ? GetString(ownerElement, "login") : null;
        return new GitHubAppInfo(appId, slug, GetString(root, "name") ?? slug, owner, GetString(root, "html_url"));
    }

    private static GitHubInstallationInfo? ReadInstallation(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) || !id.TryGetInt64(out var installationId))
            return null;

        var account = item.TryGetProperty("account", out var accountElement) ? accountElement : default;
        var login = account.ValueKind == JsonValueKind.Object ? GetString(account, "login") : null;
        var suspended = item.TryGetProperty("suspended_at", out var suspendedAt) && suspendedAt.ValueKind == JsonValueKind.String;
        return new GitHubInstallationInfo(
            installationId,
            login ?? installationId.ToString(CultureInfo.InvariantCulture),
            (account.ValueKind == JsonValueKind.Object ? GetString(account, "type") : null) ?? "User",
            GetString(item, "repository_selection") ?? "selected",
            GetString(item, "html_url"),
            suspended);
    }

    private static GitHubRepositoryInfo? ReadRepository(JsonElement item)
    {
        var fullName = GetString(item, "full_name");
        var cloneUrl = GetString(item, "clone_url");
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(cloneUrl))
            return null;

        var isPrivate = item.TryGetProperty("private", out var privateElement) && privateElement.ValueKind == JsonValueKind.True;
        return new GitHubRepositoryInfo(fullName, cloneUrl, GetString(item, "default_branch") ?? "main", isPrivate, GetString(item, "html_url"));
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

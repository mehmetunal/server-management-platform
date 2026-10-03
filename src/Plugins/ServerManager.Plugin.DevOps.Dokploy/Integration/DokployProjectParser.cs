using System.Globalization;
using System.Text.Json;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Integration;

/// <summary>
/// project.all yanıtını özetler. Yanıt veritabanı parolaları ve ortam değişkenleri içerir;
/// burada yalnız kimlik, ad, açıklama ve kaynak sayıları okunur.
/// </summary>
internal static class DokployProjectParser
{
    private static readonly string[] DatabaseKeys = ["postgres", "mysql", "mariadb", "mongo", "redis", "libsql"];

    public static IReadOnlyList<DokployProjectDto>? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var projects = new List<DokployProjectDto>();
            foreach (var project in document.RootElement.EnumerateArray())
            {
                if (project.ValueKind != JsonValueKind.Object)
                    continue;

                var environments = project.TryGetProperty("environments", out var envs) && envs.ValueKind == JsonValueKind.Array
                    ? envs.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList()
                    : [];

                // Eski sürümlerde kaynaklar doğrudan projede, yenilerde ortamların altındadır.
                var scopes = environments.Prepend(project).ToList();
                projects.Add(new DokployProjectDto
                {
                    Id = GetString(project, "projectId") ?? string.Empty,
                    Name = GetString(project, "name") ?? "(adsız)",
                    Description = NullIfEmpty(GetString(project, "description")),
                    CreatedAt = ParseDate(GetString(project, "createdAt")),
                    EnvironmentCount = environments.Count,
                    ApplicationCount = scopes.Sum(s => CountArray(s, "applications")),
                    ComposeCount = scopes.Sum(s => CountArray(s, "compose")),
                    DatabaseCount = scopes.Sum(s => DatabaseKeys.Sum(key => CountArray(s, key)))
                });
            }

            return projects.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>getDokployVersion yanıtı sürüme göre düz metin ("v0.25.3") ya da nesne olabilir.</summary>
    public static string? ParseVersion(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0)
            return null;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            var version = root.ValueKind switch
            {
                JsonValueKind.String => root.GetString(),
                JsonValueKind.Object => GetString(root, "version") ?? GetString(root, "currentVersion"),
                _ => null
            };
            return NullIfEmpty(version)?.Trim();
        }
        catch (JsonException)
        {
            return trimmed.Length <= 64 && !trimmed.Contains('<') ? trimmed : null;
        }
    }

    private static int CountArray(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTime? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed.UtcDateTime : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

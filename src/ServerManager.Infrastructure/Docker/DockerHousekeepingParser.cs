using System.Text.Json;
using ServerManager.Application.Cleanup;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Infrastructure.Docker;

/// <summary>Temizlik ve Kaynak Kullanımı sayfaları için docker çıktıları (system df -v, network ls, ps etiketleri).</summary>
internal static class DockerHousekeepingParser
{
    public sealed record DiskUsage(
        IReadOnlyList<DockerContainerFact> Containers,
        IReadOnlyList<DockerImageFact> Images,
        IReadOnlyList<DockerVolumeFact> Volumes,
        long BuildCacheReclaimableBytes,
        int BuildCacheEntries);

    /// <summary><c>docker system df -v --format '{{json .}}'</c> tek bir JSON nesnesi döner (Images, Containers, Volumes, BuildCache).</summary>
    public static DiskUsage ParseDiskUsage(string? output)
    {
        if (DockerOutputParser.ParseJsonDocument(output?.Trim()) is not { ValueKind: JsonValueKind.Object } root)
            return new DiskUsage([], [], [], 0, 0);

        var containers = Array(root, "Containers").Select(row => new DockerContainerFact(
            String(row, "ID") ?? string.Empty,
            (String(row, "Names") ?? string.Empty).Split(',')[0].Trim(),
            String(row, "Image") ?? string.Empty,
            String(row, "State") ?? string.Empty,
            String(row, "Status") ?? string.Empty,
            DockerValueParser.ParseSize(String(row, "Size")),
            ParseLabels(String(row, "Labels"))))
            .Where(c => c.Id.Length > 0)
            .ToList();

        // Kullanan container sayısı okunamazsa imaj "kullanılıyor" sayılır (-1); böylece yanlışlıkla listelenmez.
        var images = Array(root, "Images").Select(row => new DockerImageFact(
            String(row, "ID") ?? string.Empty,
            String(row, "Repository") ?? "<none>",
            String(row, "Tag") ?? "<none>",
            DockerValueParser.ParseSize(String(row, "Size")) ?? 0,
            DockerValueParser.ParseNullableInt(String(row, "Containers")) ?? -1,
            DockerValueParser.ParseTimestamp(String(row, "CreatedAt"))))
            .Where(i => i.Id.Length > 0)
            .ToList();

        var volumes = Array(root, "Volumes").Select(row => new DockerVolumeFact(
            String(row, "Name") ?? string.Empty,
            DockerValueParser.ParseNullableInt(String(row, "Links")),
            DockerValueParser.ParseSize(String(row, "Size")),
            ParseLabels(String(row, "Labels"))))
            .Where(v => v.Name.Length > 0)
            .ToList();

        long cacheBytes = 0;
        var cacheEntries = 0;
        foreach (var row in Array(root, "BuildCache"))
        {
            if (String(row, "InUse") == "true")
                continue;

            cacheEntries++;
            if (String(row, "Shared") != "true")
                cacheBytes += DockerValueParser.ParseSize(String(row, "Size")) ?? 0;
        }

        return new DiskUsage(containers, images, volumes, cacheBytes, cacheEntries);
    }

    public static IReadOnlyList<DockerNetworkFact> ParseNetworks(string? output) =>
        DockerOutputParser.ParseJsonLines(output)
            .Select(row => new DockerNetworkFact(
                String(row, "ID") ?? string.Empty,
                String(row, "Name") ?? string.Empty,
                String(row, "Driver") ?? string.Empty,
                ParseLabels(String(row, "Labels"))))
            .Where(n => n.Id.Length > 0 && n.Name.Length > 0)
            .ToList();

    /// <summary><c>docker ps -a --format '{{json .}}'</c> çıktısından container adı → etiketler.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParseContainerLabels(string? psOutput)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var row in DockerOutputParser.ParseJsonLines(psOutput))
        {
            var labels = ParseLabels(String(row, "Labels"));
            if (String(row, "ID") is { Length: > 0 } id)
                result[id] = labels;
            foreach (var name in (String(row, "Names") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                result[name] = labels;
        }

        return result;
    }

    /// <summary>docker stats satırlarını etiketlerle birleştirir (önce ad, sonra kimlik ile eşlenir).</summary>
    public static IReadOnlyList<ContainerUsageFact> JoinStats(
        IReadOnlyList<DockerContainerStatsDto> stats, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> labels) =>
        stats.Select(s => new ContainerUsageFact(s,
                labels.TryGetValue(s.Name, out var byName) ? byName
                : labels.TryGetValue(s.Id, out var byId) ? byId
                : new Dictionary<string, string>(StringComparer.Ordinal)))
            .ToList();

    /// <summary>"a=b,c=d" biçimindeki etiket metni. Değerinde virgül olan etiketler bölünür; yalnızca anahtarı bilinen etiketler kullanıldığı için sorun olmaz.</summary>
    public static IReadOnlyDictionary<string, string> ParseLabels(string? labels)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(labels))
            return result;

        foreach (var pair in labels.Split(','))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim().Length > 0)
                result[parts[0].Trim()] = parts[1].Trim();
        }

        return result;
    }

    private static IEnumerable<JsonElement> Array(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            }
            : null;
}

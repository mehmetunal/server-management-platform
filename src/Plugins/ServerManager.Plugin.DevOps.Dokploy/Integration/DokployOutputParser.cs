using System.Globalization;
using System.Text.Json;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Integration;

internal static class DokployOutputParser
{
    public const string ListenKey = "listen";

    /// <summary>"anahtar=değer" satırlarını okur; "listen" dışındaki anahtarların ilk değeri alınır.</summary>
    public static (IReadOnlyDictionary<string, string> Values, IReadOnlyList<string> Listening) ParseKeyValues(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var listening = new List<string>();
        foreach (var line in SplitLines(output))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator];
            var value = line[(separator + 1)..].Trim();
            if (key == ListenKey)
                listening.Add(value);
            else
                values.TryAdd(key, value);
        }

        return (values, listening);
    }

    /// <summary>"0.0.0.0:80", "[::]:443", ":::3000", "*:80", "127.0.0.53%lo:53" gibi adreslerden port numaralarını çıkarır.</summary>
    public static IReadOnlyList<int> ParseListeningPorts(IEnumerable<string> addresses)
    {
        var ports = new SortedSet<int>();
        foreach (var address in addresses)
        {
            var colon = address.LastIndexOf(':');
            if (colon < 0 || colon == address.Length - 1)
                continue;

            if (int.TryParse(address.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535)
                ports.Add(port);
        }

        return ports.ToList();
    }

    public static (string? Version, string? SwarmState) ParseDockerInfo(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = GetString(root, "ServerVersion");
            string? swarm = null;
            if (root.TryGetProperty("Swarm", out var swarmElement) && swarmElement.ValueKind == JsonValueKind.Object)
                swarm = GetString(swarmElement, "LocalNodeState");

            return (string.IsNullOrWhiteSpace(version) ? null : version, swarm);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    public static IReadOnlyList<DokployServiceDto> ParseServices(string output)
    {
        var services = new List<DokployServiceDto>();
        foreach (var row in ParseJsonLines(output))
        {
            var name = GetString(row, "Name") ?? string.Empty;
            if (!IsDokployName(name))
                continue;

            var (running, desired) = ParseReplicas(GetString(row, "Replicas"));
            services.Add(new DokployServiceDto
            {
                Name = name,
                Image = GetString(row, "Image") ?? string.Empty,
                RunningReplicas = running,
                DesiredReplicas = desired,
                Ports = NullIfEmpty(GetString(row, "Ports"))
            });
        }

        return services.OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<DokployContainerDto> ParseContainers(string output)
    {
        var containers = new List<DokployContainerDto>();
        foreach (var row in ParseJsonLines(output))
        {
            var name = GetString(row, "Names") ?? string.Empty;
            if (!IsDokployName(name) && !name.StartsWith("dokploy.", StringComparison.Ordinal))
                continue;

            containers.Add(new DokployContainerDto
            {
                Name = name,
                Image = GetString(row, "Image") ?? string.Empty,
                State = GetString(row, "State") ?? string.Empty,
                Status = GetString(row, "Status") ?? string.Empty
            });
        }

        return containers.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>"1/1", "0/1 (max 1 per node)" biçimindeki replika bilgisini okur.</summary>
    public static (int Running, int Desired) ParseReplicas(string? replicas)
    {
        if (string.IsNullOrWhiteSpace(replicas))
            return (0, 0);

        var token = replicas.Split(' ', 2)[0];
        var parts = token.Split('/');
        if (parts.Length != 2)
            return (0, 0);

        _ = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var running);
        _ = int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var desired);
        return (running, desired);
    }

    public static string? ParseSha256(string output)
    {
        var hash = output.Split((char[])[' ', '\t', '\n'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return hash is { Length: 64 } && hash.All(Uri.IsHexDigit) ? hash.ToLowerInvariant() : null;
    }

    public static bool IsSwarmInactive(string stderr) =>
        stderr.Contains("not a swarm manager", StringComparison.OrdinalIgnoreCase);

    public static bool IsHealthyResponse(string body) =>
        body.Contains("\"ok\":true", StringComparison.OrdinalIgnoreCase) || body.Contains("\"ok\": true", StringComparison.OrdinalIgnoreCase);

    private static bool IsDokployName(string name) =>
        name == "dokploy" || name.StartsWith("dokploy-", StringComparison.Ordinal);

    private static IEnumerable<JsonElement> ParseJsonLines(string output)
    {
        foreach (var line in SplitLines(output))
        {
            if (!line.StartsWith('{'))
                continue;

            JsonElement element;
            try
            {
                using var document = JsonDocument.Parse(line);
                element = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            yield return element;
        }
    }

    private static IEnumerable<string> SplitLines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

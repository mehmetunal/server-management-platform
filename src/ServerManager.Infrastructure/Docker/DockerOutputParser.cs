using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Infrastructure.Docker;

internal static partial class DockerOutputParser
{
    public const int MaxLogLineLength = 4000;
    public const string MaskedValue = "••••••";
    private const string ComposeProjectLabel = "com.docker.compose.project";

    private static readonly JsonSerializerOptions InspectJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static IReadOnlyList<JsonElement> ParseJsonLines(string? output)
    {
        var result = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(output))
            return result;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith('{'))
                continue;

            try
            {
                using var document = JsonDocument.Parse(line);
                result.Add(document.RootElement.Clone());
            }
            catch (JsonException)
            {
                // Uyarı satırları veya bozuk satırlar atlanır.
            }
        }

        return result;
    }

    public static JsonElement? ParseJsonDocument(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        try
        {
            using var document = JsonDocument.Parse(output);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static DockerOverviewDto ParseOverview(string infoOutput, string psOutput, string? volumeNames, string? networkIds, string? diskUsage)
    {
        var info = ParseJsonLines(infoOutput).Cast<JsonElement?>().FirstOrDefault();
        var containers = ParseJsonLines(psOutput);

        var restarting = 0;
        var failed = 0;
        var unhealthy = 0;
        foreach (var row in containers)
        {
            var state = GetString(row, "State");
            var status = GetString(row, "Status");
            if (state == "restarting")
                restarting++;
            if (state == "dead" || (state == "exited" && DockerValueParser.ParseExitCode(status) is { } code && code != 0))
                failed++;
            if (DockerValueParser.ParseHealth(status) == "unhealthy")
                unhealthy++;
        }

        return new DockerOverviewDto
        {
            EngineVersion = GetString(info, "ServerVersion"),
            OperatingSystem = GetString(info, "OperatingSystem"),
            KernelVersion = GetString(info, "KernelVersion"),
            StorageDriver = GetString(info, "Driver"),
            DockerRootDir = GetString(info, "DockerRootDir"),
            CpuCount = GetInt(info, "NCPU"),
            MemoryTotalBytes = GetLong(info, "MemTotal"),
            ContainersTotal = info is null ? containers.Count : GetInt(info, "Containers"),
            ContainersRunning = GetInt(info, "ContainersRunning"),
            ContainersStopped = GetInt(info, "ContainersStopped"),
            ContainersPaused = GetInt(info, "ContainersPaused"),
            ContainersRestarting = restarting,
            ContainersFailed = failed,
            ContainersUnhealthy = unhealthy,
            Images = GetInt(info, "Images"),
            Volumes = CountLines(volumeNames),
            Networks = CountLines(networkIds),
            DiskUsage = ParseJsonLines(diskUsage).Select(row => new DockerDiskUsageDto
            {
                Type = GetString(row, "Type") ?? string.Empty,
                TotalCount = DockerValueParser.ParseInt(GetString(row, "TotalCount")),
                ActiveCount = DockerValueParser.ParseInt(GetString(row, "Active")),
                SizeBytes = DockerValueParser.ParseSize(GetString(row, "Size")) ?? 0,
                ReclaimableBytes = DockerValueParser.ParseSize(GetString(row, "Reclaimable")) ?? 0
            }).ToList()
        };
    }

    public static IReadOnlyList<string> ParseContainerIds(string psOutput) =>
        ParseJsonLines(psOutput)
            .Select(row => GetString(row, "ID"))
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToList();

    public static IReadOnlyList<DockerContainerDto> ParseContainers(string psOutput, string? inspectOutput)
    {
        var inspected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (ParseJsonDocument(inspectOutput) is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (GetString(item, "Id") is { } id)
                    inspected[id] = item;
            }
        }

        return ParseJsonLines(psOutput)
            .Select(row =>
            {
                var id = GetString(row, "ID") ?? string.Empty;
                inspected.TryGetValue(id, out var detail);
                return BuildContainer(row, detail.ValueKind == JsonValueKind.Object ? detail : null);
            })
            .OrderBy(c => StateOrder(c.State))
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static DockerContainerDetailsDto? ParseContainerDetails(string inspectOutput)
    {
        if (ParseJsonDocument(inspectOutput) is not { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() == 0)
            return null;

        var item = array[0];
        var config = GetObject(item, "Config");
        var state = GetObject(item, "State");
        var hostConfig = GetObject(item, "HostConfig");
        var networkSettings = GetObject(item, "NetworkSettings");

        var name = (GetString(item, "Name") ?? string.Empty).TrimStart('/');
        var status = GetString(state, "Status") ?? "unknown";
        var container = new DockerContainerDto
        {
            Id = GetString(item, "Id") ?? string.Empty,
            Name = name,
            Image = GetString(config, "Image") ?? string.Empty,
            State = status,
            Status = status,
            Health = GetString(GetObject(state, "Health"), "Status"),
            Ports = string.Join(", ", ParsePortBindings(networkSettings)),
            Networks = GetObject(networkSettings, "Networks") is { } networks ? networks.EnumerateObject().Select(n => n.Name).ToList() : [],
            ComposeProject = GetLabel(config, ComposeProjectLabel),
            ExitCode = status == "exited" ? GetInt(state, "ExitCode") : null,
            RestartCount = GetInt(item, "RestartCount"),
            CreatedAt = DockerValueParser.ParseTimestamp(GetString(item, "Created")),
            StartedAt = DockerValueParser.ParseTimestamp(GetString(state, "StartedAt"))
        };

        var restartPolicy = GetObject(hostConfig, "RestartPolicy");
        var restartPolicyName = GetString(restartPolicy, "Name");
        var maxRetry = GetInt(restartPolicy, "MaximumRetryCount");

        return new DockerContainerDetailsDto
        {
            Container = container,
            Command = JoinArray(GetProperty(config, "Cmd")),
            Entrypoint = JoinArray(GetProperty(config, "Entrypoint")),
            WorkingDir = NullIfEmpty(GetString(config, "WorkingDir")),
            User = NullIfEmpty(GetString(config, "User")),
            Hostname = NullIfEmpty(GetString(config, "Hostname")),
            RestartPolicy = string.IsNullOrEmpty(restartPolicyName)
                ? null
                : maxRetry > 0 ? $"{restartPolicyName} (en fazla {maxRetry})" : restartPolicyName,
            FinishedAt = DockerValueParser.ParseTimestamp(GetString(state, "FinishedAt")),
            OomKilled = GetBool(state, "OOMKilled"),
            Error = NullIfEmpty(GetString(state, "Error")),
            Platform = NullIfEmpty(GetString(item, "Platform")),
            PortBindings = ParsePortBindings(networkSettings),
            Mounts = GetProperty(item, "Mounts") is { ValueKind: JsonValueKind.Array } mounts
                ? mounts.EnumerateArray().Select(m => new DockerMountDto
                {
                    Type = GetString(m, "Type") ?? string.Empty,
                    Name = NullIfEmpty(GetString(m, "Name")),
                    Source = GetString(m, "Source") ?? string.Empty,
                    Destination = GetString(m, "Destination") ?? string.Empty,
                    Mode = NullIfEmpty(GetString(m, "Mode")),
                    ReadWrite = GetBool(m, "RW")
                }).ToList()
                : [],
            NetworkDetails = GetObject(networkSettings, "Networks") is { } networkMap
                ? networkMap.EnumerateObject().Select(n => new DockerContainerNetworkDto
                {
                    Name = n.Name,
                    IpAddress = NullIfEmpty(GetString(n.Value, "IPAddress")),
                    Gateway = NullIfEmpty(GetString(n.Value, "Gateway")),
                    MacAddress = NullIfEmpty(GetString(n.Value, "MacAddress")),
                    Aliases = GetProperty(n.Value, "Aliases") is { ValueKind: JsonValueKind.Array } aliases
                        ? aliases.EnumerateArray().Select(a => a.GetString() ?? string.Empty).Where(a => a.Length > 0).ToList()
                        : []
                }).ToList()
                : [],
            EnvironmentKeys = GetProperty(config, "Env") is { ValueKind: JsonValueKind.Array } env
                ? env.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Select(e => e.Split('=', 2)[0])
                    .Where(k => k.Length > 0)
                    .ToList()
                : [],
            Labels = GetObject(config, "Labels") is { } labels
                ? labels.EnumerateObject().ToDictionary(l => l.Name, l => l.Value.GetString() ?? string.Empty, StringComparer.Ordinal)
                : new Dictionary<string, string>(),
            InspectJson = MaskInspect(item)
        };
    }

    /// <summary>Config.Env değerlerini maskeler; değişken adları görünür kalır.</summary>
    public static string MaskInspect(JsonElement container)
    {
        var node = JsonNode.Parse(container.GetRawText());
        if (node?["Config"]?["Env"] is JsonArray env)
        {
            for (var i = 0; i < env.Count; i++)
            {
                var value = env[i]?.GetValue<string>() ?? string.Empty;
                var key = value.Split('=', 2)[0];
                env[i] = $"{key}={MaskedValue}";
            }
        }

        return node?.ToJsonString(InspectJsonOptions) ?? string.Empty;
    }

    public static IReadOnlyList<DockerContainerStatsDto> ParseStats(string output) =>
        ParseJsonLines(output).Select(row =>
        {
            var (memoryUsage, memoryLimit) = DockerValueParser.ParseSizePair(GetString(row, "MemUsage"));
            var (rx, tx) = DockerValueParser.ParseSizePair(GetString(row, "NetIO"));
            var (blockRead, blockWrite) = DockerValueParser.ParseSizePair(GetString(row, "BlockIO"));
            return new DockerContainerStatsDto
            {
                Id = GetString(row, "ID") ?? GetString(row, "Container") ?? string.Empty,
                Name = GetString(row, "Name") ?? string.Empty,
                CpuPercent = DockerValueParser.ParsePercent(GetString(row, "CPUPerc")),
                MemoryUsageBytes = memoryUsage,
                MemoryLimitBytes = memoryLimit,
                MemoryPercent = DockerValueParser.ParsePercent(GetString(row, "MemPerc")),
                NetworkRxBytes = rx,
                NetworkTxBytes = tx,
                BlockReadBytes = blockRead,
                BlockWriteBytes = blockWrite,
                Pids = DockerValueParser.ParseInt(GetString(row, "PIDs"))
            };
        }).ToList();

    public static DockerLogsDto ParseLogs(string container, string stdout, string stderr, int tail)
    {
        var lines = ParseLogStream(stdout, false)
            .Concat(ParseLogStream(stderr, true))
            .Select((line, index) => (line, index))
            .OrderBy(x => x.line.Timestamp ?? DateTime.MinValue)
            .ThenBy(x => x.index)
            .Select(x => x.line)
            .ToList();

        var truncated = lines.Count > tail;
        if (truncated)
            lines = lines[^tail..];

        return new DockerLogsDto { Container = container, Lines = lines, Truncated = truncated };
    }

    public static IReadOnlyList<DockerImageDto> ParseImages(string output) =>
        ParseJsonLines(output).Select(row => new DockerImageDto
        {
            Id = GetString(row, "ID") ?? string.Empty,
            Repository = GetString(row, "Repository") ?? "<none>",
            Tag = GetString(row, "Tag") ?? "<none>",
            Digest = GetString(row, "Digest") is { } digest && digest != "<none>" && digest.Length > 0 ? digest : null,
            SizeBytes = DockerValueParser.ParseSize(GetString(row, "Size")) ?? 0,
            CreatedAt = DockerValueParser.ParseTimestamp(GetString(row, "CreatedAt")),
            ContainerCount = DockerValueParser.ParseNullableInt(GetString(row, "Containers"))
        })
        .OrderBy(i => i.IsDangling)
        .ThenBy(i => i.Repository, StringComparer.OrdinalIgnoreCase)
        .ThenBy(i => i.Tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static IReadOnlyList<DockerVolumeDto> ParseVolumes(string lsOutput, string? psOutput, string? diskUsageVerbose)
    {
        var sizes = new Dictionary<string, long?>(StringComparer.Ordinal);
        if (ParseJsonDocument(diskUsageVerbose) is { ValueKind: JsonValueKind.Object } usage
            && GetProperty(usage, "Volumes") is { ValueKind: JsonValueKind.Array } usageVolumes)
        {
            foreach (var volume in usageVolumes.EnumerateArray())
            {
                if (GetString(volume, "Name") is { } name)
                    sizes[name] = DockerValueParser.ParseSize(GetString(volume, "Size"));
            }
        }

        var usedBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in ParseJsonLines(psOutput))
        {
            var containerName = (GetString(row, "Names") ?? string.Empty).Split(',')[0];
            foreach (var mount in (GetString(row, "Mounts") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!usedBy.TryGetValue(mount, out var list))
                    usedBy[mount] = list = [];
                list.Add(containerName);
            }
        }

        return ParseJsonLines(lsOutput).Select(row =>
        {
            var name = GetString(row, "Name") ?? string.Empty;
            return new DockerVolumeDto
            {
                Name = name,
                Driver = GetString(row, "Driver") ?? string.Empty,
                Mountpoint = GetString(row, "Mountpoint") ?? string.Empty,
                Scope = GetString(row, "Scope") ?? string.Empty,
                SizeBytes = sizes.TryGetValue(name, out var size) ? size : null,
                Containers = usedBy.TryGetValue(name, out var containers) ? containers : [],
                ComposeProject = ParseLabelValue(GetString(row, "Labels"), ComposeProjectLabel)
            };
        })
        .OrderBy(v => v.Containers.Count == 0)
        .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    public static IReadOnlyList<string> ParseNetworkIds(string lsOutput) =>
        ParseJsonLines(lsOutput)
            .Select(row => GetString(row, "ID"))
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToList();

    public static IReadOnlyList<DockerNetworkDto> ParseNetworks(string lsOutput, string? inspectOutput)
    {
        var inspected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (ParseJsonDocument(inspectOutput) is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (GetString(item, "Id") is { } id)
                    inspected[id] = item;
            }
        }

        return ParseJsonLines(lsOutput).Select(row =>
        {
            var id = GetString(row, "ID") ?? string.Empty;
            inspected.TryGetValue(id, out var detail);
            var hasDetail = detail.ValueKind == JsonValueKind.Object;
            var ipamConfig = hasDetail && GetObject(detail, "IPAM") is { } ipam && GetProperty(ipam, "Config") is { ValueKind: JsonValueKind.Array } config
                ? config.EnumerateArray().ToList()
                : [];

            return new DockerNetworkDto
            {
                Id = id,
                Name = GetString(row, "Name") ?? string.Empty,
                Driver = GetString(row, "Driver") ?? string.Empty,
                Scope = GetString(row, "Scope") ?? string.Empty,
                Internal = GetString(row, "Internal") == "true",
                CreatedAt = DockerValueParser.ParseTimestamp(GetString(row, "CreatedAt")),
                Subnets = ipamConfig.Select(c => GetString(c, "Subnet")).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList(),
                Gateways = ipamConfig.Select(c => GetString(c, "Gateway")).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList(),
                Containers = hasDetail && GetObject(detail, "Containers") is { } containers
                    ? containers.EnumerateObject().Select(c => new DockerNetworkContainerDto
                    {
                        Name = GetString(c.Value, "Name") ?? c.Name,
                        IpAddress = NullIfEmpty(GetString(c.Value, "IPv4Address"))
                    }).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList()
                    : []
            };
        })
        .OrderBy(n => n.IsSystem)
        .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    public static string StripAnsi(string value) => AnsiRegex().Replace(value, string.Empty);

    private static DockerContainerDto BuildContainer(JsonElement row, JsonElement? detail)
    {
        var state = GetString(row, "State") ?? "unknown";
        var status = GetString(row, "Status") ?? string.Empty;
        var detailState = detail is { } d ? GetObject(d, "State") : null;
        var detailConfig = detail is { } c ? GetObject(c, "Config") : null;

        return new DockerContainerDto
        {
            Id = GetString(row, "ID") ?? string.Empty,
            Name = (GetString(row, "Names") ?? string.Empty).Split(',')[0],
            Image = GetString(row, "Image") ?? string.Empty,
            State = state,
            Status = status,
            Health = GetString(GetObject(detailState, "Health"), "Status") ?? DockerValueParser.ParseHealth(status),
            Ports = GetString(row, "Ports") ?? string.Empty,
            Networks = (GetString(row, "Networks") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ComposeProject = GetLabel(detailConfig, ComposeProjectLabel) ?? ParseLabelValue(GetString(row, "Labels"), ComposeProjectLabel),
            ExitCode = state == "exited" ? (detailState is null ? DockerValueParser.ParseExitCode(status) : GetInt(detailState, "ExitCode")) : null,
            RestartCount = detail is { } r ? GetInt(r, "RestartCount") : 0,
            CreatedAt = DockerValueParser.ParseTimestamp(detail is { } created ? GetString(created, "Created") : GetString(row, "CreatedAt")),
            StartedAt = DockerValueParser.ParseTimestamp(GetString(detailState, "StartedAt"))
        };
    }

    private static IReadOnlyList<string> ParsePortBindings(JsonElement? networkSettings)
    {
        if (GetObject(networkSettings, "Ports") is not { } ports)
            return [];

        var result = new List<string>();
        foreach (var port in ports.EnumerateObject())
        {
            if (port.Value.ValueKind != JsonValueKind.Array)
            {
                result.Add(port.Name);
                continue;
            }

            foreach (var binding in port.Value.EnumerateArray())
            {
                var hostIp = GetString(binding, "HostIp");
                var hostPort = GetString(binding, "HostPort");
                result.Add($"{(string.IsNullOrEmpty(hostIp) ? "0.0.0.0" : hostIp)}:{hostPort} → {port.Name}");
            }
        }

        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<DockerLogLineDto> ParseLogStream(string output, bool isError)
    {
        if (string.IsNullOrEmpty(output))
            yield break;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                continue;

            var space = line.IndexOf(' ');
            var rawTimestamp = space > 0 ? line[..space] : null;
            var timestamp = DockerValueParser.ParseTimestamp(rawTimestamp);
            var text = timestamp is null ? line : line[(space + 1)..];
            text = StripAnsi(text);
            if (text.Length > MaxLogLineLength)
                text = text[..MaxLogLineLength] + "…";

            yield return new DockerLogLineDto
            {
                Timestamp = timestamp,
                RawTimestamp = timestamp is null ? null : rawTimestamp,
                Text = text,
                IsError = isError
            };
        }
    }

    private static int StateOrder(string state) => state switch
    {
        "running" => 0,
        "restarting" => 1,
        "paused" => 2,
        "created" => 3,
        "exited" => 4,
        "dead" => 5,
        _ => 6
    };

    private static int CountLines(string? output) =>
        string.IsNullOrWhiteSpace(output) ? 0 : output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static string? GetLabel(JsonElement? config, string key) =>
        GetObject(config, "Labels") is { } labels && labels.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? NullIfEmpty(value.GetString())
            : null;

    private static string? ParseLabelValue(string? labels, string key)
    {
        if (string.IsNullOrEmpty(labels))
            return null;

        foreach (var pair in labels.Split(','))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim() == key)
                return NullIfEmpty(parts[1].Trim());
        }

        return null;
    }

    private static string? JoinArray(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.Array } array => NullIfEmpty(string.Join(' ', array.EnumerateArray().Select(e => e.GetString()))),
        { ValueKind: JsonValueKind.String } text => NullIfEmpty(text.GetString()),
        _ => null
    };

    private static JsonElement? GetProperty(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var value) ? value : null;

    private static JsonElement? GetObject(JsonElement? element, string name) =>
        GetProperty(element, name) is { ValueKind: JsonValueKind.Object } value ? value : null;

    private static string? GetString(JsonElement? element, string name) => GetProperty(element, name) switch
    {
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        { ValueKind: JsonValueKind.Number } value => value.GetRawText(),
        { ValueKind: JsonValueKind.True } => "true",
        { ValueKind: JsonValueKind.False } => "false",
        _ => null
    };

    private static int GetInt(JsonElement? element, string name) =>
        GetProperty(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var result) ? result : 0;

    private static long GetLong(JsonElement? element, string name) =>
        GetProperty(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var result) ? result : 0;

    private static bool GetBool(JsonElement? element, string name) =>
        GetProperty(element, name) is { ValueKind: JsonValueKind.True };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~]|\][^\x07]*\x07)")]
    private static partial Regex AnsiRegex();
}

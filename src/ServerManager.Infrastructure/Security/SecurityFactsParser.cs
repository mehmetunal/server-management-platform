using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using ServerManager.Application.Security;

namespace ServerManager.Infrastructure.Security;

internal static partial class SecurityFactsParser
{
    private static readonly string[] SshKeys =
    [
        "permitrootlogin", "passwordauthentication", "permitemptypasswords", "maxauthtries", "x11forwarding",
        "pubkeyauthentication", "kbdinteractiveauthentication", "challengeresponseauthentication", "port", "allowusers", "allowgroups"
    ];

    public static SecurityFacts Parse(string output)
    {
        var sections = SplitSections(output);
        if (!sections.TryGetValue("meta", out var meta))
            throw new FormatException("Tarama çıktısında meta bölümü yok.");

        var metaValues = KeyValues(meta);
        var isRoot = metaValues.GetValueOrDefault("uid") == "0";
        var prettyName = meta.FirstOrDefault(l => l.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));

        var (portsAvailable, ports) = ParsePorts(Section(sections, "ports"));
        return new SecurityFacts
        {
            IsRoot = isRoot,
            Kernel = metaValues.GetValueOrDefault("kernel"),
            OperatingSystem = prettyName is null ? null : prettyName["PRETTY_NAME=".Length..].Trim('"'),
            Ssh = ParseSsh(Section(sections, "sshd_t"), Section(sections, "sshd_config")),
            PortsAvailable = portsAvailable,
            Ports = ports,
            Firewall = ParseFirewall(Section(sections, "firewall"), isRoot),
            FailedLogins = ParseAuth(Section(sections, "auth")),
            Docker = ParseDocker(Section(sections, "docker")),
            Updates = ParseUpdates(Section(sections, "updates")),
            Disk = ParseDisk(Section(sections, "disk")),
            Users = ParseUsers(Section(sections, "passwd"), Section(sections, "groups"), Section(sections, "shadow"), Section(sections, "nopasswd"))
        };
    }

    public static bool IsComplete(string output) => output.Contains(SecurityScanScript.EndMarker, StringComparison.Ordinal);

    internal static SshFacts ParseSsh(IReadOnlyList<string> effective, IReadOnlyList<string> config)
    {
        var effectiveSettings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in effective)
        {
            var (key, value) = SplitDirective(line);
            if (key is not null && SshKeys.Contains(key))
                effectiveSettings.TryAdd(key, value);
        }

        if (effectiveSettings.ContainsKey("permitrootlogin"))
            return new SshFacts { Source = SshFacts.EffectiveSource, Settings = effectiveSettings };

        // sshd ilk bulduğu değeri kullanır; Match blokları yalnızca eşleşen bağlantılara uygulanır.
        var fileSettings = new Dictionary<string, string>(StringComparer.Ordinal);
        var inMatch = false;
        var anyFile = false;
        foreach (var line in config)
        {
            if (line.StartsWith("##file ", StringComparison.Ordinal))
            {
                inMatch = false;
                anyFile = true;
                continue;
            }

            var (key, value) = SplitDirective(line);
            if (key is null)
                continue;
            if (key == "match")
            {
                inMatch = true;
                continue;
            }

            if (!inMatch && SshKeys.Contains(key))
                fileSettings.TryAdd(key, value);
        }

        return anyFile ? new SshFacts { Source = SshFacts.ConfigFileSource, Settings = fileSettings } : new SshFacts();
    }

    internal static (bool Available, IReadOnlyList<ListeningPort> Ports) ParsePorts(IReadOnlyList<string> lines)
    {
        var tool = lines.FirstOrDefault(l => l.StartsWith("tool=", StringComparison.Ordinal))?["tool=".Length..];
        if (tool is null)
            return (false, []);

        var ports = new List<ListeningPort>();
        foreach (var line in lines)
        {
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 4)
                continue;

            var port = tool == "ss" ? ParseSsLine(columns) : ParseNetstatLine(columns);
            if (port is not null && !ports.Any(p => p.Protocol == port.Protocol && p.Address == port.Address && p.Port == port.Port))
                ports.Add(port);
        }

        return (true, ports);
    }

    private static ListeningPort? ParseSsLine(string[] columns)
    {
        // Netid State Recv-Q Send-Q Local:Port Peer:Port [Process]
        if (columns.Length < 5)
            return null;
        var protocol = columns[0].ToLowerInvariant();
        var state = columns[1].ToUpperInvariant();
        if (!(protocol == "tcp" && state == "LISTEN") && !(protocol == "udp" && state is "UNCONN" or "LISTEN"))
            return null;

        var process = columns.Length > 6 ? SsProcessRegex().Match(string.Join(' ', columns.Skip(6))) : Match.Empty;
        return BuildPort(protocol, columns[4], process.Success ? process.Groups[1].Value : null);
    }

    private static ListeningPort? ParseNetstatLine(string[] columns)
    {
        // Proto Recv-Q Send-Q Local Foreign [State] [PID/Program]
        var protocol = columns[0].ToLowerInvariant();
        if (protocol is not ("tcp" or "tcp6" or "udp" or "udp6"))
            return null;

        var isTcp = protocol.StartsWith("tcp", StringComparison.Ordinal);
        if (isTcp && !columns.Contains("LISTEN"))
            return null;

        // Program adı boşluk içerebilir (busybox: "14/sshd -e [listener]").
        var processStart = isTcp ? Array.IndexOf(columns, "LISTEN") + 1 : 5;
        string? process = null;
        if (processStart > 0 && processStart < columns.Length)
        {
            var slash = columns[processStart].IndexOf('/');
            if (slash > 0 && slash < columns[processStart].Length - 1)
                process = columns[processStart][(slash + 1)..];
        }

        return BuildPort(isTcp ? "tcp" : "udp", columns[3], process);
    }

    private static ListeningPort? BuildPort(string protocol, string local, string? process)
    {
        var separator = local.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(local[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port))
            return null;

        var address = local[..separator];
        if (address.StartsWith('[') && address.EndsWith(']'))
            address = address[1..^1];
        var zone = address.IndexOf('%');
        if (zone >= 0)
            address = address[..zone];

        return new ListeningPort
        {
            Protocol = protocol,
            Address = address,
            Port = port,
            Process = process,
            Exposure = ClassifyAddress(address)
        };
    }

    internal static PortExposure ClassifyAddress(string address)
    {
        if (address is "*" or "0.0.0.0" or "::" or "")
            return PortExposure.AllInterfaces;

        if (address.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            address = address[7..];

        if (!IPAddress.TryParse(address, out var ip))
            return PortExposure.PublicAddress;
        if (IPAddress.IsLoopback(ip))
            return PortExposure.Loopback;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            var isPrivate = bytes[0] == 10
                            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                            || (bytes[0] == 192 && bytes[1] == 168)
                            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                            || (bytes[0] == 169 && bytes[1] == 254);
            return isPrivate ? PortExposure.PrivateAddress : PortExposure.PublicAddress;
        }

        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal ? PortExposure.PrivateAddress : PortExposure.PublicAddress;
    }

    internal static FirewallFacts ParseFirewall(IReadOnlyList<string> lines, bool isRoot)
    {
        var tools = new List<FirewallTool>();
        foreach (var (name, body) in Blocks(lines, "##tool "))
        {
            tools.Add(name switch
            {
                "ufw" => ParseUfw(body),
                "firewalld" => new FirewallTool
                {
                    Name = name,
                    Active = body.Any(l => l.Trim() == "running") ? true : body.Any(l => l.Contains("not running", StringComparison.Ordinal)) ? false : null
                },
                "nftables" => ParseNftables(body, isRoot),
                "iptables" => ParseIptables(body),
                _ => new FirewallTool { Name = name }
            });
        }

        return new FirewallFacts { Tools = tools };
    }

    private static FirewallTool ParseUfw(IReadOnlyList<string> body)
    {
        var status = body.FirstOrDefault(l => l.StartsWith("Status:", StringComparison.Ordinal))?["Status:".Length..].Trim();
        var rules = body.Where(l => UfwRuleRegex().IsMatch(l)).Select(l => RegexWhitespace().Replace(l.Trim(), " ")).Take(50).ToList();
        return new FirewallTool
        {
            Name = "ufw",
            Active = status switch { "active" => true, "inactive" => false, _ => null },
            Detail = status == "active" ? $"{rules.Count} kural" : null,
            Rules = rules
        };
    }

    private static FirewallTool ParseNftables(IReadOnlyList<string> body, bool isRoot)
    {
        if (!body.Any(l => l.TrimStart().StartsWith("table ", StringComparison.Ordinal)))
        {
            var empty = isRoot && body.All(string.IsNullOrWhiteSpace);
            return new FirewallTool { Name = "nftables", Active = empty ? false : null };
        }

        var inputRules = 0;
        var dropPolicy = false;
        var depth = 0;
        var chainDepth = -1;
        var isInput = false;
        foreach (var raw in body)
        {
            var line = raw.Trim();
            var braces = line.Count(c => c == '{') - line.Count(c => c == '}');
            if (line.StartsWith("chain ", StringComparison.Ordinal))
            {
                depth += braces;
                chainDepth = depth;
                isInput = false;
                continue;
            }

            if (chainDepth >= 0 && depth == chainDepth)
            {
                if (line.StartsWith("type ", StringComparison.Ordinal))
                {
                    isInput = line.Contains("hook input", StringComparison.Ordinal);
                    if (isInput && (line.Contains("policy drop", StringComparison.Ordinal) || line.Contains("policy reject", StringComparison.Ordinal)))
                        dropPolicy = true;
                }
                else if (isInput && line.Length > 0 && line != "}")
                {
                    inputRules++;
                }
            }

            depth += braces;
            if (depth < chainDepth)
            {
                chainDepth = -1;
                isInput = false;
            }
        }

        return new FirewallTool
        {
            Name = "nftables",
            Active = dropPolicy || inputRules > 0,
            Detail = dropPolicy ? $"input: policy drop, {inputRules} kural" : $"input: {inputRules} kural"
        };
    }

    private static FirewallTool ParseIptables(IReadOnlyList<string> body)
    {
        var policy = body.FirstOrDefault(l => l.StartsWith("-P INPUT ", StringComparison.Ordinal));
        if (policy is null)
            return new FirewallTool { Name = "iptables" };

        var dropPolicy = policy.EndsWith("DROP", StringComparison.Ordinal) || policy.EndsWith("REJECT", StringComparison.Ordinal);
        var rules = body.Where(l => l.StartsWith("-A INPUT", StringComparison.Ordinal)).Take(50).ToList();
        return new FirewallTool
        {
            Name = "iptables",
            Active = dropPolicy || rules.Count > 0,
            Detail = $"INPUT: {policy["-P INPUT ".Length..].Trim()}, {rules.Count} kural",
            Rules = rules
        };
    }

    internal static FailedLoginFacts ParseAuth(IReadOnlyList<string> lines)
    {
        var values = KeyValues(lines);
        var source = values.GetValueOrDefault("source");
        int? total = int.TryParse(values.GetValueOrDefault("total"), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;
        var top = lines
            .Select(l => LoginSourceRegex().Match(l))
            .Where(m => m.Success)
            .Select(m => new LoginSourceCount(m.Groups[2].Value, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)))
            .ToList();

        return new FailedLoginFacts
        {
            Source = string.IsNullOrEmpty(source) ? null : source,
            Total = string.IsNullOrEmpty(source) ? null : total,
            TopSources = top,
            Fail2BanActive = values.GetValueOrDefault("fail2ban") switch { "active" => true, "inactive" => false, _ => null }
        };
    }

    internal static DockerFacts ParseDocker(IReadOnlyList<string> lines)
    {
        var values = KeyValues(lines);
        if (values.GetValueOrDefault("installed") != "1")
            return new DockerFacts();

        var hosts = new List<string>();
        var tlsVerify = false;
        foreach (var line in lines.Where(l => l.StartsWith("proc=", StringComparison.Ordinal)))
        {
            foreach (Match match in DaemonHostRegex().Matches(line))
                hosts.Add(match.Groups[1].Value);
            if (line.Contains("--tlsverify", StringComparison.Ordinal))
                tlsVerify = true;
        }

        var json = string.Join('\n', Blocks(lines, "##daemon.json").SelectMany(b => b.Body).TakeWhile(l => l != "##end"));
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (document.RootElement.TryGetProperty("hosts", out var hostArray) && hostArray.ValueKind == JsonValueKind.Array)
                        hosts.AddRange(hostArray.EnumerateArray().Where(h => h.ValueKind == JsonValueKind.String).Select(h => h.GetString()!));
                    if (document.RootElement.TryGetProperty("tlsverify", out var verify) && verify.ValueKind == JsonValueKind.True)
                        tlsVerify = true;
                }
            }
            catch (JsonException)
            {
                // Bozuk daemon.json taramayı durdurmaz; yalnızca komut satırındaki -H değerleri kullanılır.
            }
        }

        var published = new List<PublishedPort>();
        foreach (var line in lines.Where(l => l.StartsWith("ctr=", StringComparison.Ordinal)))
        {
            var value = line["ctr=".Length..];
            var separator = value.IndexOf('|');
            if (separator <= 0)
                continue;

            var container = value[..separator];
            foreach (Match match in PublishedPortRegex().Matches(value[(separator + 1)..]))
            {
                var address = match.Groups["addr"].Value.Trim('[', ']');
                var port = new PublishedPort(container, address,
                    int.Parse(match.Groups["host"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["ctr"].Value, CultureInfo.InvariantCulture),
                    match.Groups["proto"].Value);
                if (!published.Any(p => p.Container == port.Container && p.HostPort == port.HostPort && p.Protocol == port.Protocol))
                    published.Add(port);
            }
        }

        return new DockerFacts
        {
            Installed = true,
            Accessible = values.GetValueOrDefault("access") == "1",
            DaemonHosts = hosts.Distinct(StringComparer.Ordinal).ToList(),
            TlsVerify = tlsVerify,
            PublishedPorts = published
        };
    }

    internal static UpdateFacts ParseUpdates(IReadOnlyList<string> lines)
    {
        var values = KeyValues(lines);
        var manager = values.GetValueOrDefault("manager");
        var auto = values.GetValueOrDefault("auto");
        return new UpdateFacts
        {
            Manager = manager,
            Pending = ParseInt(values.GetValueOrDefault("pending")),
            Security = ParseInt(values.GetValueOrDefault("security")),
            RebootRequired = values.GetValueOrDefault("reboot") == "1",
            AutoUpdates = auto switch
            {
                null => null,
                "none" or "0" => false,
                "1" => true,
                _ => auto.Contains("\"1\"", StringComparison.Ordinal)
            }
        };
    }

    internal static DiskFacts ParseDisk(IReadOnlyList<string> lines)
    {
        var values = KeyValues(lines);
        if (values.GetValueOrDefault("checked") != "1")
            return new DiskFacts();

        var devices = lines
            .Where(l => !l.Contains('=') && (l.Contains(" crypt", StringComparison.Ordinal) || l.Contains("crypto_LUKS", StringComparison.Ordinal)))
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new DiskFacts { Checked = true, EncryptedDevices = devices };
    }

    internal static UserFacts ParseUsers(IReadOnlyList<string> passwd, IReadOnlyList<string> groups, IReadOnlyList<string> shadow, IReadOnlyList<string> nopasswd)
    {
        var accounts = new List<UserAccount>();
        foreach (var line in passwd)
        {
            var parts = line.Split(':');
            if (parts.Length >= 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
                accounts.Add(new UserAccount(parts[0], uid, parts[2]));
        }

        var sudoMembers = groups
            .Select(l => l.Split(':'))
            .Where(p => p.Length >= 4)
            .SelectMany(p => p[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new UserFacts
        {
            Accounts = accounts,
            SudoMembers = sudoMembers,
            ShadowReadable = shadow.Contains("readable=1"),
            EmptyPasswordUsers = shadow.Where(l => l.StartsWith("empty=", StringComparison.Ordinal)).Select(l => l["empty=".Length..]).ToList(),
            NoPasswordSudoRules = nopasswd.Select(l => RegexWhitespace().Replace(l.Trim(), " ")).Where(l => l.Length > 0).ToList()
        };
    }

    private static Dictionary<string, List<string>> SplitSections(string output)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var raw in output.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith(SecurityScanScript.SectionPrefix, StringComparison.Ordinal))
            {
                current = [];
                sections[line[SecurityScanScript.SectionPrefix.Length..]] = current;
                continue;
            }

            current?.Add(line);
        }

        return sections;
    }

    private static IReadOnlyList<string> Section(Dictionary<string, List<string>> sections, string name) =>
        sections.TryGetValue(name, out var lines) ? lines : [];

    private static Dictionary<string, string> KeyValues(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf('=');
            if (separator > 0 && !line.StartsWith('#'))
                values.TryAdd(line[..separator], line[(separator + 1)..].Trim());
        }

        return values;
    }

    private static IEnumerable<(string Name, IReadOnlyList<string> Body)> Blocks(IReadOnlyList<string> lines, string prefix)
    {
        string? name = null;
        var body = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (name is not null)
                    yield return (name, body);
                name = line[prefix.Length..].Trim();
                body = [];
                continue;
            }

            if (name is not null)
                body.Add(line);
        }

        if (name is not null)
            yield return (name, body);
    }

    private static (string? Key, string Value) SplitDirective(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            return (null, string.Empty);

        var separator = trimmed.IndexOfAny([' ', '\t', '=']);
        return separator <= 0
            ? (trimmed.ToLowerInvariant(), string.Empty)
            : (trimmed[..separator].ToLowerInvariant(), trimmed[(separator + 1)..].Trim().TrimStart('=').Trim());
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    [GeneratedRegex("""\(\("([^"]+)""")]
    private static partial Regex SsProcessRegex();

    [GeneratedRegex(@"\b(ALLOW|DENY|REJECT|LIMIT)\b")]
    private static partial Regex UfwRuleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexWhitespace();

    [GeneratedRegex(@"^\s*(\d+)\s+from\s+(\S+)")]
    private static partial Regex LoginSourceRegex();

    [GeneratedRegex(@"(?:-H|--host)[= ](\S+)")]
    private static partial Regex DaemonHostRegex();

    [GeneratedRegex(@"(?<addr>\[[0-9a-fA-F:]*\]|[0-9.]+|::):(?<host>\d+)(?:-\d+)?->(?<ctr>\d+)(?:-\d+)?/(?<proto>tcp|udp|sctp)")]
    private static partial Regex PublishedPortRegex();
}

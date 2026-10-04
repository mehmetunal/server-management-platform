using System.Globalization;
using System.Text.RegularExpressions;
using ServerManager.Application.ServerSystem;
using ServerManager.Infrastructure.Security;

namespace ServerManager.Infrastructure.ServerSystem;

internal static partial class ServerSystemParser
{
    public const int MaxProcesses = 500;

    private static readonly HashSet<string> PseudoFileSystems = new(StringComparer.Ordinal)
    {
        "proc", "sysfs", "devpts", "cgroup", "cgroup2", "mqueue", "debugfs", "tracefs", "securityfs", "pstore",
        "bpf", "configfs", "fusectl", "hugetlbfs", "autofs", "binfmt_misc", "nsfs", "squashfs", "efivarfs", "rpc_pipefs"
    };

    public static bool IsComplete(string output) => output.Contains(ServerSystemCommands.SectionPrefix + "end", StringComparison.Ordinal);

    public static ServiceList ParseServices(string output)
    {
        var sections = SplitSections(output);
        if (sections.ContainsKey("systemd"))
        {
            var enabled = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var line in Section(sections, "unitfiles"))
            {
                var columns = Columns(line);
                if (columns.Length >= 2)
                    enabled[columns[0]] = columns[1] is "enabled" or "enabled-runtime" or "alias";
            }

            var units = new List<ServiceUnit>();
            foreach (var line in Section(sections, "systemd"))
            {
                var columns = line.TrimStart('●', '*', ' ').Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 4 || !columns[0].EndsWith(".service", StringComparison.Ordinal))
                    continue;

                units.Add(new ServiceUnit(
                    columns[0],
                    columns.Length > 4 ? columns[4].Trim() : null,
                    columns[2],
                    columns[3],
                    enabled.TryGetValue(columns[0], out var isEnabled) ? isEnabled : null));
            }

            return new ServiceList(ServiceManagerKind.Systemd, units.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }

        if (sections.ContainsKey("openrc"))
        {
            var enabled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in Section(sections, "enabled"))
            {
                var parts = line.Split('|', 2);
                if (parts.Length == 2 && parts[1].Trim().Length > 0)
                    enabled.Add(parts[0].Trim());
            }

            var units = new Dictionary<string, ServiceUnit>(StringComparer.Ordinal);
            foreach (var line in Section(sections, "openrc"))
            {
                var match = OpenRcLine().Match(line);
                if (!match.Success)
                    continue;

                var name = match.Groups[1].Value;
                var state = match.Groups[2].Value.ToLowerInvariant();
                units.TryAdd(name, new ServiceUnit(name, null, state == "started" ? "active" : state == "crashed" ? "failed" : "inactive", state, enabled.Contains(name)));
            }

            return new ServiceList(ServiceManagerKind.OpenRc, units.Values.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }

        return new ServiceList(ServiceManagerKind.None, []);
    }

    public static ProcessList ParseProcesses(string output)
    {
        var sections = SplitSections(output);
        var processes = new List<ProcessEntry>();

        if (sections.ContainsKey("procps"))
        {
            foreach (var line in Section(sections, "procps"))
            {
                var columns = line.Trim().Split(' ', 8, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 8 || !int.TryParse(columns[0], out var pid))
                    continue;

                processes.Add(new ProcessEntry(
                    pid,
                    ParseInt(columns[1]),
                    columns[2],
                    ParseDouble(columns[3]),
                    ParseDouble(columns[4]),
                    ParseLong(columns[5]),
                    ParseLong(columns[6]),
                    columns[7].Trim()));
            }

            return new ProcessList(processes.Take(MaxProcesses).ToList(), true, processes.Count);
        }

        foreach (var line in Section(sections, "busybox"))
        {
            var columns = line.Trim().Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 5 || !int.TryParse(columns[0], out var pid))
                continue;

            processes.Add(new ProcessEntry(pid, ParseInt(columns[1]), columns[2], null, null, ParseSizeKilobytes(columns[3]), null, columns[4].Trim()));
        }

        var ordered = processes.OrderByDescending(p => p.ResidentKilobytes ?? 0).ToList();
        return new ProcessList(ordered.Take(MaxProcesses).ToList(), false, ordered.Count);
    }

    public static (bool HasJournal, IReadOnlyList<string> Files) ParseLogSources(string output)
    {
        var sections = SplitSections(output);
        var hasJournal = Section(sections, "journal").Any(l => l.Trim() == "yes");
        var files = Section(sections, "files")
            .Select(l => l.Trim())
            .Where(ServerSystemRules.IsValidLogPath)
            .ToList();
        return (hasJournal, files);
    }

    public static IReadOnlyList<string> SplitLines(string output)
    {
        var lines = output.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    public static NetworkSnapshot ParseNetwork(string output)
    {
        var sections = SplitSections(output);
        var interfaces = new Dictionary<string, (string? State, string? Mac, int? Mtu, List<string> Addresses)>(StringComparer.Ordinal);

        foreach (var line in Section(sections, "link"))
        {
            var match = LinkLine().Match(line);
            if (!match.Success)
                continue;

            var name = StripPeer(match.Groups[1].Value);
            var state = StateValue().Match(line);
            var mac = MacValue().Match(line);
            var mtu = MtuValue().Match(line);
            interfaces[name] = (
                state.Success ? state.Groups[1].Value : null,
                mac.Success && mac.Groups[1].Value != "00:00:00:00:00:00" ? mac.Groups[1].Value : null,
                mtu.Success ? ParseInt(mtu.Groups[1].Value) : null,
                []);
        }

        foreach (var line in Section(sections, "addr"))
        {
            var columns = Columns(line);
            if (columns.Length < 4 || !columns[0].EndsWith(':') || columns[2] is not ("inet" or "inet6"))
                continue;

            var name = StripPeer(columns[1]);
            if (!interfaces.TryGetValue(name, out var entry))
            {
                entry = (null, null, null, []);
                interfaces[name] = entry;
            }

            entry.Addresses.Add(columns[3]);
        }

        var counters = new Dictionary<string, (long Rx, long Tx)>(StringComparer.Ordinal);
        foreach (var line in Section(sections, "dev"))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var name = line[..separator].Trim();
            var values = Columns(line[(separator + 1)..]);
            if (values.Length >= 9 && long.TryParse(values[0], out var rx) && long.TryParse(values[8], out var tx))
            {
                counters[name] = (rx, tx);
                if (!interfaces.ContainsKey(name))
                    interfaces[name] = (null, null, null, []);
            }
        }

        var items = interfaces
            .Select(kv => new NetworkInterfaceEntry(
                kv.Key,
                kv.Value.State,
                kv.Value.Mac,
                kv.Value.Mtu,
                kv.Value.Addresses,
                counters.TryGetValue(kv.Key, out var c) ? c.Rx : null,
                counters.TryGetValue(kv.Key, out var c2) ? c2.Tx : null))
            .OrderBy(i => i.Name == "lo" ? 1 : 0)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        var dns = Section(sections, "dns")
            .Select(l => Columns(l))
            .Where(c => c.Length >= 2 && c[0] == "nameserver")
            .Select(c => c[1])
            .ToList();

        var (portsAvailable, ports) = SecurityFactsParser.ParsePorts(Section(sections, "ports"));

        return new NetworkSnapshot(
            Section(sections, "hostname").Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0),
            items,
            Section(sections, "route").Select(l => l.Trim()).Where(l => l.Length > 0).ToList(),
            dns,
            portsAvailable,
            ports.OrderBy(p => p.Port).ThenBy(p => p.Protocol, StringComparer.Ordinal).ToList());
    }

    public static StorageSnapshot ParseStorage(string output)
    {
        var sections = SplitSections(output);

        var inodes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in Section(sections, "inodes").Skip(1))
        {
            var columns = Columns(line);
            if (columns.Length >= 6 && TryParsePercent(columns[4], out var percent))
                inodes[string.Join(' ', columns[5..])] = percent;
        }

        var dfLines = Section(sections, "df");
        var hasType = dfLines.FirstOrDefault()?.Contains("Type", StringComparison.Ordinal) == true;
        var fileSystems = new List<FileSystemEntry>();
        foreach (var line in dfLines.Skip(1))
        {
            var columns = Columns(line);
            var offset = hasType ? 1 : 0;
            if (columns.Length < 6 + offset)
                continue;

            var type = hasType ? columns[1] : null;
            if (type is not null && PseudoFileSystems.Contains(type))
                continue;
            if (!long.TryParse(columns[1 + offset], out var size) || size == 0)
                continue;

            var mount = string.Join(' ', columns[(5 + offset)..]);
            fileSystems.Add(new FileSystemEntry(
                columns[0],
                type,
                size,
                ParseLong(columns[2 + offset]) ?? 0,
                ParseLong(columns[3 + offset]) ?? 0,
                TryParsePercent(columns[4 + offset], out var use) ? use : 0,
                mount,
                inodes.TryGetValue(mount, out var inode) ? inode : null));
        }

        var lsblk = Section(sections, "lsblk");
        var devices = new List<BlockDeviceEntry>();
        foreach (var line in lsblk)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in KeyValuePair().Matches(line))
                values[match.Groups[1].Value] = match.Groups[2].Value;

            if (!values.TryGetValue("NAME", out var name) || !values.TryGetValue("TYPE", out var deviceType))
                continue;

            devices.Add(new BlockDeviceEntry(
                name,
                deviceType,
                values.TryGetValue("SIZE", out var sizeText) ? ParseLong(sizeText) : null,
                NullIfEmpty(values.GetValueOrDefault("MOUNTPOINT")),
                NullIfEmpty(values.GetValueOrDefault("FSTYPE")),
                NullIfEmpty(values.GetValueOrDefault("MODEL")?.Trim())));
        }

        return new StorageSnapshot(
            fileSystems.OrderBy(f => f.MountPoint, StringComparer.Ordinal).ToList(),
            devices.Count > 0,
            devices.Where(d => d.Type is not "loop").ToList());
    }

    internal static long? ParseSizeKilobytes(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var multiplier = char.ToLowerInvariant(value[^1]) switch
        {
            'k' => 1L,
            'm' => 1024L,
            'g' => 1024L * 1024,
            't' => 1024L * 1024 * 1024,
            _ => 0L
        };

        if (multiplier == 0)
            return ParseLong(value);

        return double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? (long)Math.Round(number * multiplier)
            : null;
    }

    private static string StripPeer(string name)
    {
        var trimmed = name.TrimEnd(':');
        var at = trimmed.IndexOf('@');
        return at > 0 ? trimmed[..at] : trimmed;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string[] Columns(string line) => line.Split(' ', '\t').Where(c => c.Length > 0).ToArray();

    private static bool TryParsePercent(string value, out int percent) =>
        int.TryParse(value.TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent);

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static long? ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static Dictionary<string, List<string>> SplitSections(string output)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var raw in output.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith(ServerSystemCommands.SectionPrefix, StringComparison.Ordinal))
            {
                current = [];
                sections[line[ServerSystemCommands.SectionPrefix.Length..]] = current;
                continue;
            }

            current?.Add(line);
        }

        return sections;
    }

    private static IReadOnlyList<string> Section(Dictionary<string, List<string>> sections, string name) =>
        sections.TryGetValue(name, out var lines) ? lines : [];

    [GeneratedRegex(@"^\s*(\S+)\s+\[\s*(\w+)\s*\]")]
    private static partial Regex OpenRcLine();

    [GeneratedRegex(@"^\d+:\s+(\S+?):?\s+<")]
    private static partial Regex LinkLine();

    [GeneratedRegex(@"\bstate\s+(\S+)")]
    private static partial Regex StateValue();

    [GeneratedRegex(@"link/\w+\s+([0-9a-fA-F:]{17})")]
    private static partial Regex MacValue();

    [GeneratedRegex(@"\bmtu\s+(\d+)")]
    private static partial Regex MtuValue();

    [GeneratedRegex("([A-Z]+)=\"([^\"]*)\"")]
    private static partial Regex KeyValuePair();
}

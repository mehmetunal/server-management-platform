using System.Globalization;
using ServerManager.Application.DTOs.Monitoring;

namespace ServerManager.Infrastructure.Monitoring;

internal static class LinuxMetricsParser
{
    private const int MaxProcesses = 10;

    private static readonly HashSet<string> IgnoredFileSystemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "tmpfs", "devtmpfs", "udev", "none", "shm", "proc", "sysfs", "cgroup", "cgroup2", "squashfs", "efivarfs", "devfs", "run"
    };

    private static readonly string[] IgnoredMountPrefixes = ["/proc", "/sys", "/dev", "/run", "/snap", "/boot/efi"];

    // Container ortamlarında tek dosya bind mount'ları disk gibi listelenir.
    private static readonly HashSet<string> IgnoredMountPoints = new(StringComparer.Ordinal)
    {
        "/etc/hosts", "/etc/hostname", "/etc/resolv.conf"
    };

    private static readonly string[] VirtualInterfacePrefixes = ["lo", "veth", "docker", "br-", "virbr", "cni", "flannel", "cali"];

    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.Ordinal) { "ps", "head", "sh", "sleep" };

    public static SystemMetricsSnapshot Parse(string output, DateTime nowUtc)
    {
        var sections = SplitSections(output);
        if (!sections.ContainsKey(LinuxMetricsScript.EndMarker.TrimStart('@')))
            throw new FormatException("Metrik çıktısı eksik.");

        var stat1 = Line(sections, "STAT1") ?? throw new FormatException("/proc/stat okunamadı.");
        var stat2 = Line(sections, "STAT2") ?? throw new FormatException("/proc/stat okunamadı.");
        if (!sections.TryGetValue("MEMINFO", out var memInfoLines) || memInfoLines.Count == 0)
            throw new FormatException("/proc/meminfo okunamadı.");

        var uptime1 = ParseUptime(Line(sections, "T1"));
        var uptime2 = ParseUptime(Line(sections, "T2"));
        var intervalSeconds = uptime2 - uptime1;
        if (intervalSeconds <= 0.05)
            intervalSeconds = 1;

        var threads = ParseInt(Line(sections, "THREADS"));
        var cores = ParseInt(Line(sections, "CORES"));

        var snapshot = new SystemMetricsSnapshot
        {
            CpuUsagePercent = CalculateCpuUsage(stat1, stat2),
            CpuThreads = threads,
            CpuCores = cores > 0 ? cores : threads,
            CpuTemperatureCelsius = ParseTemperature(Line(sections, "TEMP"))
        };

        ApplyLoadAverage(snapshot, Line(sections, "LOADAVG"));
        ApplyMemInfo(snapshot, memInfoLines);
        snapshot.Disks = ParseDisks(Lines(sections, "DF"), Lines(sections, "DFI"));
        snapshot.NetworkInterfaces = ParseNetwork(Lines(sections, "NET1"), Lines(sections, "NET2"), intervalSeconds, Lines(sections, "ADDR"));
        snapshot.TopProcesses = ParseProcesses(Lines(sections, "PS"));

        var uptimeSeconds = (long)uptime2;
        snapshot.System = new SystemInfo
        {
            Hostname = Line(sections, "HOSTNAME"),
            Kernel = Line(sections, "KERNEL"),
            Architecture = Line(sections, "ARCH"),
            OperatingSystem = ParseOsRelease(Lines(sections, "OSRELEASE")),
            Timezone = ParseTimezone(Line(sections, "TZ"), Line(sections, "TZABBR")),
            UptimeSeconds = uptimeSeconds,
            BootTimeUtc = uptimeSeconds > 0 ? nowUtc.AddSeconds(-uptimeSeconds) : null
        };

        return snapshot;
    }

    internal static bool IsVirtualInterface(string name) =>
        VirtualInterfacePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    internal static double CalculateCpuUsage(string stat1, string stat2)
    {
        var (total1, idle1) = ParseCpuLine(stat1);
        var (total2, idle2) = ParseCpuLine(stat2);
        var totalDelta = total2 - total1;
        var idleDelta = idle2 - idle1;
        if (totalDelta <= 0)
            return 0;

        var usage = (totalDelta - idleDelta) * 100d / totalDelta;
        return Math.Round(Math.Clamp(usage, 0, 100), 1);
    }

    private static (long Total, long Idle) ParseCpuLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || parts[0] != "cpu")
            throw new FormatException("/proc/stat formatı tanınmadı.");

        // user nice system idle iowait irq softirq steal; guest değerleri user içinde zaten sayılıyor.
        var values = parts.Skip(1).Take(8).Select(ParseLong).ToArray();
        var total = values.Sum();
        var idle = values[3] + (values.Length > 4 ? values[4] : 0);
        return (total, idle);
    }

    private static void ApplyLoadAverage(SystemMetricsSnapshot snapshot, string? line)
    {
        if (line is null)
            return;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return;

        snapshot.LoadAverage1 = ParseDouble(parts[0]);
        snapshot.LoadAverage5 = ParseDouble(parts[1]);
        snapshot.LoadAverage15 = ParseDouble(parts[2]);
    }

    private static void ApplyMemInfo(SystemMetricsSnapshot snapshot, IReadOnlyList<string> lines)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var key = line[..separator];
            var valueParts = line[(separator + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (valueParts.Length == 0)
                continue;

            var multiplier = valueParts.Length > 1 && valueParts[1] == "kB" ? 1024L : 1L;
            values[key] = ParseLong(valueParts[0]) * multiplier;
        }

        long Get(string key) => values.GetValueOrDefault(key);

        snapshot.MemoryTotalBytes = Get("MemTotal");
        snapshot.MemoryFreeBytes = Get("MemFree");
        snapshot.MemoryCachedBytes = Get("Cached") + Get("Buffers");
        snapshot.MemoryAvailableBytes = values.TryGetValue("MemAvailable", out var available)
            ? available
            : Get("MemFree") + Get("Buffers") + Get("Cached");
        snapshot.SwapTotalBytes = Get("SwapTotal");
        snapshot.SwapFreeBytes = Get("SwapFree");
    }

    internal static List<DiskUsageInfo> ParseDisks(IReadOnlyList<string> dfLines, IReadOnlyList<string> inodeLines)
    {
        var inodeUsage = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var line in inodeLines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                continue;

            var mount = string.Join(' ', parts.Skip(5));
            var percentText = parts[4].TrimEnd('%');
            inodeUsage[mount] = double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                ? percent
                : null;
        }

        var disks = new List<DiskUsageInfo>();
        var seenFileSystems = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in dfLines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                continue;

            var fileSystem = parts[0];
            var mount = string.Join(' ', parts.Skip(5));
            if (IgnoredFileSystemTypes.Contains(fileSystem)
                || IgnoredMountPoints.Contains(mount)
                || IgnoredMountPrefixes.Any(prefix => mount == prefix || mount.StartsWith(prefix + "/", StringComparison.Ordinal)))
                continue;

            var totalKb = ParseLong(parts[1]);
            var usedKb = ParseLong(parts[2]);
            var availableKb = ParseLong(parts[3]);
            if (totalKb <= 0)
                continue;

            // Docker gibi ortamlarda aynı aygıt bind mount'larla tekrar listelenir; ilkini tut.
            if (!seenFileSystems.Add(fileSystem)
                || disks.Any(d => d.TotalBytes == totalKb * 1024 && d.UsedBytes == usedKb * 1024 && d.AvailableBytes == availableKb * 1024))
                continue;

            disks.Add(new DiskUsageInfo
            {
                FileSystem = fileSystem,
                MountPoint = mount,
                TotalBytes = totalKb * 1024,
                UsedBytes = usedKb * 1024,
                AvailableBytes = availableKb * 1024,
                UsagePercent = SystemMetricsSnapshot.Percent(usedKb, usedKb + availableKb),
                InodeUsagePercent = inodeUsage.GetValueOrDefault(mount)
            });
        }

        return disks;
    }

    internal static List<NetworkInterfaceInfo> ParseNetwork(
        IReadOnlyList<string> before, IReadOnlyList<string> after, double intervalSeconds, IReadOnlyList<string> addressLines)
    {
        var first = ParseNetDev(before);
        var second = ParseNetDev(after);
        var addresses = ParseAddresses(addressLines);

        var interfaces = new List<NetworkInterfaceInfo>();
        foreach (var (name, current) in second)
        {
            var interfaceAddresses = addresses.GetValueOrDefault(name) ?? [];
            // Kernel'in otomatik oluşturduğu kullanılmayan tünel arayüzleri (gre0, sit0, tunl0...) gösterilmez.
            if (current.RxBytes == 0 && current.TxBytes == 0 && interfaceAddresses.Count == 0)
                continue;

            var previous = first.GetValueOrDefault(name, current);
            interfaces.Add(new NetworkInterfaceInfo
            {
                Name = name,
                IsVirtual = IsVirtualInterface(name),
                RxBytes = current.RxBytes,
                TxBytes = current.TxBytes,
                RxPackets = current.RxPackets,
                TxPackets = current.TxPackets,
                RxBytesPerSecond = Math.Round(Math.Max(0, current.RxBytes - previous.RxBytes) / intervalSeconds, 1),
                TxBytesPerSecond = Math.Round(Math.Max(0, current.TxBytes - previous.TxBytes) / intervalSeconds, 1),
                Addresses = interfaceAddresses
            });
        }

        return interfaces.OrderBy(i => i.IsVirtual).ThenBy(i => i.Name, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, (long RxBytes, long RxPackets, long TxBytes, long TxPackets)> ParseNetDev(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, (long, long, long, long)>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var name = line[..separator].Trim();
            var values = line[(separator + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 10 || name.Length == 0)
                continue;

            result[name] = (ParseLong(values[0]), ParseLong(values[1]), ParseLong(values[8]), ParseLong(values[9]));
        }

        return result;
    }

    private static Dictionary<string, List<string>> ParseAddresses(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                continue;

            var familyIndex = Array.FindIndex(parts, p => p is "inet" or "inet6");
            if (familyIndex < 1 || familyIndex + 1 >= parts.Length)
                continue;

            var name = parts[1].TrimEnd(':').Split('@')[0];
            if (!result.TryGetValue(name, out var list))
                result[name] = list = [];
            list.Add(parts[familyIndex + 1]);
        }

        return result;
    }

    internal static List<ProcessUsageInfo> ParseProcesses(IReadOnlyList<string> lines)
    {
        var processes = new List<ProcessUsageInfo>();
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;

            var name = string.Join(' ', parts.Skip(4));
            if (IgnoredProcesses.Contains(name))
                continue;

            processes.Add(new ProcessUsageInfo
            {
                Pid = pid,
                User = parts[1],
                CpuPercent = ParseDouble(parts[2]),
                MemoryPercent = ParseDouble(parts[3]),
                Name = name
            });

            if (processes.Count == MaxProcesses)
                break;
        }

        return processes;
    }

    private static string? ParseOsRelease(IReadOnlyList<string> lines)
    {
        string? name = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                return line["PRETTY_NAME=".Length..].Trim('"');
            if (line.StartsWith("NAME=", StringComparison.Ordinal))
                name = line["NAME=".Length..].Trim('"');
        }

        return name;
    }

    private static string? ParseTimezone(string? zone, string? abbreviation)
    {
        if (!string.IsNullOrWhiteSpace(zone))
        {
            const string zoneInfo = "zoneinfo/";
            var index = zone.IndexOf(zoneInfo, StringComparison.Ordinal);
            return index >= 0 ? zone[(index + zoneInfo.Length)..] : zone;
        }

        return string.IsNullOrWhiteSpace(abbreviation) ? null : abbreviation;
    }

    private static double? ParseTemperature(string? line)
    {
        if (line is null || !double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return null;

        return Math.Round(value >= 1000 ? value / 1000 : value, 1);
    }

    private static double ParseUptime(string? line)
    {
        var first = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is null ? 0 : ParseDouble(first);
    }

    private static Dictionary<string, List<string>> SplitSections(string output)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("@@", StringComparison.Ordinal) && line.Length > 2 && !line.Contains(' '))
            {
                current = [];
                sections[line[2..]] = current;
                continue;
            }

            if (current is not null && !string.IsNullOrWhiteSpace(line))
                current.Add(line.Trim());
        }

        return sections;
    }

    private static string? Line(Dictionary<string, List<string>> sections, string key) =>
        sections.TryGetValue(key, out var lines) && lines.Count > 0 ? lines[0] : null;

    private static IReadOnlyList<string> Lines(Dictionary<string, List<string>> sections, string key) =>
        sections.TryGetValue(key, out var lines) ? lines : [];

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static int ParseInt(string? value) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
}

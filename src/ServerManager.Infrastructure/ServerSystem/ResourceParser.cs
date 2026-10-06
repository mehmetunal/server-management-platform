using System.Globalization;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Infrastructure.ServerSystem;

/// <summary><see cref="ResourceCommands"/> çıktıları.</summary>
internal static class ResourceParser
{
    public const int MaxSwapProcesses = 10;
    public const int MaxIoProcesses = 10;

    public static ResourceSnapshot ParseSnapshot(string output)
    {
        var sections = SectionedOutput.Parse(output);
        var load = sections.Lines("loadavg").FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var uptime = sections.Lines("uptime").FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var oomSource = sections.Value("oom", "source");

        return new ResourceSnapshot
        {
            CpuCores = sections.Lines("nproc").Select(l => ParseInt(l.Trim())).FirstOrDefault(n => n > 0),
            Load1 = load.Length > 0 ? ParseDouble(load[0]) : null,
            Load5 = load.Length > 1 ? ParseDouble(load[1]) : null,
            Load15 = load.Length > 2 ? ParseDouble(load[2]) : null,
            UptimeSeconds = uptime.Length > 0 && ParseDouble(uptime[0]) is { } seconds ? (long)seconds : null,
            Memory = ParseMemInfo(sections["meminfo"]),
            Cpu = ParseCpu(sections["cpu"]),
            SwapProcesses = ParseSwap(sections["swap"]),
            OomEvents = sections.Lines("oom").Where(l => !l.StartsWith("source=", StringComparison.Ordinal)).Select(l => l.Trim()).ToList(),
            OomSource = oomSource,
            IoToolAvailable = sections.Value("io", "tool") is not null,
            IoProcesses = ParsePidstat(sections["io"])
        };
    }

    internal static MemoryInfo? ParseMemInfo(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var number = line[(separator + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (number is not null && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                values[line[..separator].Trim()] = value;
        }

        if (!values.TryGetValue("MemTotal", out var total) || total <= 0)
            return null;

        var free = values.GetValueOrDefault("MemFree");
        var buffers = values.GetValueOrDefault("Buffers");
        var cached = values.GetValueOrDefault("Cached") + values.GetValueOrDefault("SReclaimable");
        // Eski çekirdeklerde (3.14 öncesi) MemAvailable yoktur; boş + önbellek yaklaşık değerdir.
        var available = values.TryGetValue("MemAvailable", out var reported) ? reported : free + buffers + cached;
        return new MemoryInfo(total, available, free, buffers, cached, values.GetValueOrDefault("Shmem"),
            values.GetValueOrDefault("SwapTotal"), values.GetValueOrDefault("SwapFree"));
    }

    /// <summary>vmstat'ın son satırı (başlıktaki us/sy/id/wa/st sütunlarına göre) veya /proc/stat'ın iki örneği arasındaki fark.</summary>
    internal static CpuBreakdown? ParseCpu(IReadOnlyList<string> lines)
    {
        var tool = lines.Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("tool=", StringComparison.Ordinal))?[5..];
        return tool == "vmstat" ? ParseVmstat(lines) : ParseProcStat(lines);
    }

    internal static CpuBreakdown? ParseVmstat(IReadOnlyList<string> lines)
    {
        string[]? header = null;
        string[]? last = null;
        foreach (var line in lines)
        {
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Contains("us") && columns.Contains("id"))
                header = columns;
            else if (header is not null && columns.Length == header.Length && columns.All(c => long.TryParse(c, out _)))
                last = columns;
        }

        if (header is null || last is null)
            return null;

        double Column(string name) => Array.IndexOf(header, name) is var index and >= 0 ? ParseDouble(last[index]) ?? 0 : 0;
        return new CpuBreakdown(Column("us"), Column("sy"), Column("id"), Column("wa"), Column("st"), "vmstat");
    }

    internal static CpuBreakdown? ParseProcStat(IReadOnlyList<string> lines)
    {
        var samples = lines
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(c => c.Length >= 5 && c[0] == "cpu")
            .Select(c => c.Skip(1).Select(v => long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0).ToArray())
            .ToList();
        if (samples.Count < 2)
            return null;

        var first = samples[^2];
        var second = samples[^1];
        var length = Math.Min(first.Length, second.Length);
        var delta = Enumerable.Range(0, length).Select(i => Math.Max(0, second[i] - first[i])).ToArray();
        double total = delta.Sum();
        if (total <= 0)
            return null;

        long At(int index) => index < delta.Length ? delta[index] : 0;
        // user nice system idle iowait irq softirq steal …
        return new CpuBreakdown(
            Percent(At(0) + At(1), total),
            Percent(At(2) + At(5) + At(6), total),
            Percent(At(3), total),
            Percent(At(4), total),
            Percent(At(7), total),
            "/proc/stat");
    }

    /// <summary>grep -H çıktısı: "/proc/123/status:Name:\tnginx" ve "/proc/123/status:VmSwap:  2048 kB".</summary>
    internal static IReadOnlyList<SwapProcess> ParseSwap(IEnumerable<string> lines)
    {
        var names = new Dictionary<int, string>();
        var swaps = new Dictionary<int, long>();
        foreach (var line in lines)
        {
            if (!line.StartsWith("/proc/", StringComparison.Ordinal))
                continue;

            var slash = line.IndexOf('/', 6);
            if (slash < 0 || !int.TryParse(line.AsSpan(6, slash - 6), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;

            var colon = line.IndexOf(':', slash);
            if (colon < 0)
                continue;

            var rest = line[(colon + 1)..];
            if (rest.StartsWith("Name:", StringComparison.Ordinal))
                names[pid] = rest[5..].Trim();
            else if (rest.StartsWith("VmSwap:", StringComparison.Ordinal)
                     && rest[7..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } value
                     && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb) && kb > 0)
                swaps[pid] = kb;
        }

        return swaps
            .OrderByDescending(s => s.Value)
            .Take(MaxSwapProcesses)
            .Select(s => new SwapProcess(s.Key, names.GetValueOrDefault(s.Key, "?"), s.Value))
            .ToList();
    }

    /// <summary>pidstat -d çıktısının "Average:" satırları; sütunlar başlıktan bulunur (sysstat sürümüne göre UID / iodelay olmayabilir).</summary>
    internal static IReadOnlyList<IoProcess> ParsePidstat(IEnumerable<string> lines)
    {
        string[]? header = null;
        var result = new List<IoProcess>();
        foreach (var line in lines)
        {
            if (!line.StartsWith("Average:", StringComparison.Ordinal))
                continue;

            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            if (columns.Contains("PID") && columns.Contains("kB_rd/s"))
            {
                header = columns;
                continue;
            }

            if (header is null)
                continue;

            var pidIndex = Array.IndexOf(header, "PID");
            var readIndex = Array.IndexOf(header, "kB_rd/s");
            var writeIndex = Array.IndexOf(header, "kB_wr/s");
            var commandIndex = Array.IndexOf(header, "Command");
            var userIndex = Array.IndexOf(header, "UID") is var uid and >= 0 ? uid : Array.IndexOf(header, "USER");
            if (commandIndex < 0 || columns.Length <= commandIndex || pidIndex < 0 || readIndex < 0 || writeIndex < 0)
                continue;
            if (!int.TryParse(columns[pidIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;

            var read = ParseDouble(columns[readIndex]) ?? 0;
            var write = ParseDouble(columns[writeIndex]) ?? 0;
            if (read <= 0 && write <= 0)
                continue;

            result.Add(new IoProcess(pid, userIndex >= 0 ? columns[userIndex] : null, string.Join(' ', columns[commandIndex..]), read, write));
        }

        return result.OrderByDescending(p => p.TotalKilobytesPerSecond).Take(MaxIoProcesses).ToList();
    }

    public static DiskUsageReport ParseDiskScan(string path, string output)
    {
        var sections = SectionedOutput.Parse(output);
        return new DiskUsageReport(
            path,
            ParseDu(path, sections["du"]),
            ParseLargeFiles(sections["files"]),
            IsTimeout(sections.Lines("dustatus").FirstOrDefault()),
            IsTimeout(sections.Lines("filesstatus").FirstOrDefault()),
            ResourceRules.ScanTimeoutSeconds);
    }

    /// <summary>du -k çıktısı ("boyut\tyol"); taranan yolun kendisi derinlik 0'dır.</summary>
    internal static IReadOnlyList<DirectoryUsage> ParseDu(string root, IEnumerable<string> lines)
    {
        var rootDepth = Depth(root);
        var result = new List<DirectoryUsage>();
        foreach (var line in lines)
        {
            var separator = line.IndexOfAny(['\t', ' ']);
            if (separator <= 0 || !long.TryParse(line[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                continue;

            var path = line[(separator + 1)..].Trim();
            if (!path.StartsWith('/'))
                continue;

            result.Add(new DirectoryUsage(path, size, Math.Max(0, Depth(path) - rootDepth)));
        }

        return result.OrderByDescending(d => d.SizeKilobytes).Take(ResourceRules.MaxDirectories).ToList();
    }

    /// <summary>stat çıktısı: "boyut|mtime|sahip|yol" (yolda '|' olabilir; ilk üç alan ayrılır).</summary>
    internal static IReadOnlyList<LargeFile> ParseLargeFiles(IEnumerable<string> lines)
    {
        var result = new List<LargeFile>();
        foreach (var line in lines)
        {
            var parts = line.Trim().Split('|', 4);
            if (parts.Length != 4 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) || !parts[3].StartsWith('/'))
                continue;

            DateTime? modified = long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null;
            result.Add(new LargeFile(parts[3], size, modified, parts[2].Length > 0 ? parts[2] : null));
        }

        return result.OrderByDescending(f => f.SizeBytes).Take(ResourceRules.MaxFiles).ToList();
    }

    /// <summary>GNU timeout 124 ile, BusyBox timeout sinyal koduyla (143 = SIGTERM, 137 = SIGKILL) çıkar.</summary>
    internal static bool IsTimeout(string? status) => status?.Trim() is "124" or "143" or "137";

    private static int Depth(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

    private static double Percent(long part, double total) => Math.Round(part * 100d / total, 1);

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;
}

using System.Globalization;
using ServerManager.Application.ResourceUsage;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Infrastructure.ServerSystem;

/// <summary><see cref="ResourceHistoryCommands"/> çıktılarını ayrıştırır.</summary>
public static class ResourceHistoryParser
{
    public static bool IsComplete(string? output) =>
        output is not null && output.Contains(ResourceHistoryCommands.SectionPrefix + "end", StringComparison.Ordinal);

    public static bool HasDocker(string? output) =>
        output is not null && !output.Contains(ResourceHistoryCommands.SectionPrefix + "nodocker", StringComparison.Ordinal);

    /// <summary>Process listesi ve sunucunun toplam CPU kullanımı.</summary>
    public static (IReadOnlyList<ProcessSample> Processes, double? CpuBusyPercent) ParseProcesses(string output)
    {
        var sections = SplitSections(output);
        var clockTicks = ParseFirstInt(sections, "clk") is { } clk and > 0 ? clk : 100;
        var cpuCount = ParseFirstInt(sections, "ncpu") is { } n and > 0 ? n : 1;

        var cpu0 = ParseCpuLine(First(sections, "cpu0"));
        var cpu1 = ParseCpuLine(First(sections, "cpu1"));
        double? busy = null;
        double? elapsedSeconds = null;
        if (cpu0 is { } a && cpu1 is { } b && b.Total > a.Total)
        {
            var total = b.Total - a.Total;
            var idle = b.Idle - a.Idle;
            busy = Math.Clamp(100d * (total - idle) / total, 0, 100);
            elapsedSeconds = total / (double)(clockTicks * cpuCount);
        }

        var before = ParseStatLines(Section(sections, "t0"));
        var after = ParseStatLines(Section(sections, "t1"));
        var ps = ParsePs(Section(sections, "ps"));

        var processes = new List<ProcessSample>();
        if (after.Count > 0 && elapsedSeconds is > 0)
        {
            foreach (var (pid, stat) in after)
            {
                // Arada başlayan process'in tüm CPU zamanı bu aralığa sayılır.
                var previous = before.TryGetValue(pid, out var old) ? old.Ticks : 0;
                var cpu = Math.Max(0, stat.Ticks - previous) * 100d / (clockTicks * elapsedSeconds.Value);
                ps.TryGetValue(pid, out var info);
                processes.Add(new ProcessSample(
                    pid,
                    info?.User ?? string.Empty,
                    Math.Round(cpu, 1),
                    info?.MemoryPercent ?? 0,
                    info?.ResidentKilobytes ?? 0,
                    info is null ? stat.Name : NameOf(info.Command, stat.Name),
                    info?.Command ?? $"[{stat.Name}]"));
            }
        }
        else
        {
            // /proc yoksa (ör. Linux dışı) ps'in ömür boyu ortalaması kullanılır.
            processes.AddRange(ps.Values.Select(p => new ProcessSample(
                p.Pid, p.User, p.CpuPercent, p.MemoryPercent, p.ResidentKilobytes, NameOf(p.Command, p.Command), p.Command)));
        }

        return (processes, busy);
    }

    public static IReadOnlyList<ContainerStateFact> ParseContainers(string output)
    {
        var sections = SplitSections(output);
        var stats = DockerOutputParser.ParseStats(string.Join('\n', Section(sections, "stats")))
            .Where(s => s.Name.Length > 0)
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var result = new List<ContainerStateFact>();
        foreach (var line in Section(sections, "inspect"))
        {
            var columns = line.Split('|');
            if (columns.Length < 3)
                continue;

            var name = columns[0].Trim().TrimStart('/');
            if (name.Length == 0)
                continue;

            var restarts = int.TryParse(columns[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
            var state = columns[2].Trim();
            var health = columns.Length > 3 && columns[3].Trim().Length > 0 ? columns[3].Trim() : null;
            stats.TryGetValue(name, out var usage);
            result.Add(new ContainerStateFact(
                name,
                state.Length == 0 ? "unknown" : state,
                health,
                restarts,
                usage?.CpuPercent ?? 0,
                usage?.MemoryUsageBytes ?? 0,
                usage?.MemoryLimitBytes ?? 0,
                usage?.MemoryPercent ?? 0,
                usage?.NetworkRxBytes ?? 0,
                usage?.NetworkTxBytes ?? 0,
                usage?.BlockReadBytes ?? 0,
                usage?.BlockWriteBytes ?? 0));
        }

        return result;
    }

    private sealed record StatEntry(string Name, long Ticks);

    private sealed record PsEntry(int Pid, string User, double CpuPercent, double MemoryPercent, long ResidentKilobytes, string Command);

    private readonly record struct CpuTimes(long Total, long Idle);

    /// <summary><c>cpu  user nice system idle iowait irq softirq steal …</c>; boşta = idle + iowait.</summary>
    private static CpuTimes? ParseCpuLine(string? line)
    {
        if (line is null || !line.StartsWith("cpu", StringComparison.Ordinal))
            return null;

        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8)
            .Select(v => long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0)
            .ToArray();
        if (values.Length < 4)
            return null;

        var idle = values[3] + (values.Length > 4 ? values[4] : 0);
        return new CpuTimes(values.Sum(), idle);
    }

    /// <summary><c>pid (comm) state ppid … utime stime …</c>; comm boşluk veya parantez içerebilir, son ')' sonrası alanlar okunur.</summary>
    private static Dictionary<int, StatEntry> ParseStatLines(IEnumerable<string> lines)
    {
        var result = new Dictionary<int, StatEntry>();
        foreach (var line in lines)
        {
            var open = line.IndexOf(" (", StringComparison.Ordinal);
            var close = line.LastIndexOf(')');
            if (open <= 0 || close <= open || !int.TryParse(line.AsSpan(0, open), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;

            var fields = line[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 13
                || !long.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var utime)
                || !long.TryParse(fields[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stime))
                continue;

            result[pid] = new StatEntry(line[(open + 2)..close], utime + stime);
        }

        return result;
    }

    private static Dictionary<int, PsEntry> ParsePs(IEnumerable<string> lines)
    {
        var result = new Dictionary<int, PsEntry>();
        foreach (var line in lines)
        {
            var columns = line.Trim().Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 6 || !int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;

            result[pid] = new PsEntry(
                pid,
                columns[1],
                ParseDouble(columns[2]),
                ParseDouble(columns[3]),
                long.TryParse(columns[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rss) ? rss : 0,
                columns[5].Trim());
        }

        return result;
    }

    /// <summary>Komutun ilk parçasının dosya adı; çekirdek iş parçacıkları köşeli parantezle gelir.</summary>
    private static string NameOf(string command, string fallback)
    {
        if (string.IsNullOrWhiteSpace(command))
            return fallback;

        var first = command.Split(' ', 2)[0];
        if (first.StartsWith('['))
            return command.Trim();

        var slash = first.LastIndexOf('/');
        var name = slash >= 0 && slash < first.Length - 1 ? first[(slash + 1)..] : first;
        return name.TrimEnd(':');
    }

    private static double ParseDouble(string value) =>
        double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static int? ParseFirstInt(Dictionary<string, List<string>> sections, string name) =>
        First(sections, name) is { } line && int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string? First(Dictionary<string, List<string>> sections, string name) =>
        Section(sections, name).FirstOrDefault(l => l.Trim().Length > 0);

    private static IEnumerable<string> Section(Dictionary<string, List<string>> sections, string name) =>
        sections.TryGetValue(name, out var lines) ? lines : [];

    private static Dictionary<string, List<string>> SplitSections(string output)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var raw in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.StartsWith(ResourceHistoryCommands.SectionPrefix, StringComparison.Ordinal))
            {
                var name = raw[ResourceHistoryCommands.SectionPrefix.Length..].Trim();
                current = new List<string>();
                sections[name] = current;
                continue;
            }

            if (raw.Length > 0)
                current?.Add(raw);
        }

        return sections;
    }
}

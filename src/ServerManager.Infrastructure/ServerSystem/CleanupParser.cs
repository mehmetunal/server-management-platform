using System.Globalization;
using System.Text.RegularExpressions;
using ServerManager.Application.Cleanup;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Infrastructure.ServerSystem;

/// <summary>Temizlik tarama betiğinin (<see cref="CleanupCommands.Scan"/>) çıktısı.</summary>
internal static partial class CleanupParser
{
    public sealed record SystemFacts(
        string? PackageManager,
        long? PackageCacheBytes,
        bool JournalAvailable,
        long? JournalBytes,
        FileSetFact RotatedLogs,
        FileSetFact TempFiles,
        bool SnapAvailable,
        IReadOnlyList<SnapRevisionFact> DisabledSnaps,
        string? RunningKernel,
        IReadOnlyList<KernelFact> Kernels);

    public static SystemFacts ParseSystem(string output)
    {
        var sections = SectionedOutput.Parse(output);
        var (manager, cacheBytes) = ParsePackageManager(sections.Lines("pm").FirstOrDefault());
        var journalLines = sections.Lines("journal").ToList();
        var journalAvailable = journalLines.Any(l => l.Trim() == "available");
        var snapLines = sections.Lines("snap").ToList();
        var snapAvailable = snapLines.Any(l => l.Trim() == "available");

        return new SystemFacts(
            manager,
            cacheBytes,
            journalAvailable,
            journalAvailable ? ParseJournalUsage(string.Join('\n', journalLines)) : null,
            ParseFileSet(sections["logs"]),
            ParseFileSet(sections["tmp"]),
            snapAvailable,
            snapAvailable ? ParseDisabledSnaps(snapLines, sections["snapfiles"]) : [],
            sections.Lines("kernel").Select(l => l.Trim()).FirstOrDefault(),
            ParseKernels(sections["kernels"]));
    }

    internal static (string? Manager, long? CacheBytes) ParsePackageManager(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return (null, null);

        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts[0] is not ("apt" or "dnf" or "yum"))
            return (null, null);

        return (parts[0], parts.Length > 1 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb) ? kb * 1024 : null);
    }

    /// <summary>
    /// "Archived and active journals take up 1.2G in the file system." / "Journals take up 48.0M on disk." → bayt.
    /// Hiç journal yoksa ("No journal files were found.") 0.
    /// </summary>
    internal static long? ParseJournalUsage(string output)
    {
        var match = JournalUsage().Match(output);
        if (match.Success)
            return ParseUnitSize(match.Groups["number"].Value, match.Groups["unit"].Value);

        return output.Contains("No journal files", StringComparison.OrdinalIgnoreCase) ? 0 : null;
    }

    internal static FileSetFact ParseFileSet(IReadOnlyList<string> lines)
    {
        var count = 0;
        long total = 0;
        var largest = new List<FileFact>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith(CleanupCommands.TotalMarker, StringComparison.Ordinal))
            {
                var totals = line[CleanupCommands.TotalMarker.Length..].Split('|');
                if (totals.Length == 2)
                {
                    count = (int)Math.Min(int.MaxValue, ParseLong(totals[0]) ?? 0);
                    total = ParseLong(totals[1]) ?? 0;
                }

                continue;
            }

            var parts = line.Split('|', 3);
            if (parts.Length == 3 && ParseLong(parts[0]) is { } size && parts[2].StartsWith('/'))
                largest.Add(new FileFact(parts[2], size, ParseUnixTime(parts[1])));
        }

        return new FileSetFact(count, total, largest);
    }

    /// <summary><c>snap list --all</c>: Notes sütununda "disabled" olan revizyonlar; boyut /var/lib/snapd/snaps/ad_rev.snap dosyasından.</summary>
    internal static IReadOnlyList<SnapRevisionFact> ParseDisabledSnaps(IEnumerable<string> listLines, IEnumerable<string> fileLines)
    {
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in fileLines)
        {
            var parts = line.Trim().Split('|', 2);
            if (parts.Length == 2 && ParseLong(parts[0]) is { } size)
                sizes[Path.GetFileNameWithoutExtension(parts[1])] = size;
        }

        var result = new List<SnapRevisionFact>();
        foreach (var line in listLines)
        {
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 4 || columns[0] == "Name")
                continue;
            if (!columns[^1].Split(',').Contains("disabled", StringComparer.Ordinal))
                continue;

            var name = columns[0];
            var revision = columns[2];
            if (!SnapName().IsMatch(name) || !SnapRevision().IsMatch(revision))
                continue;

            result.Add(new SnapRevisionFact(name, revision, sizes.TryGetValue($"{name}_{revision}", out var size) ? size : null));
        }

        return result;
    }

    /// <summary>dpkg-query (Installed-Size KiB) veya rpm (SIZE bayt) çıktısı; meta paketler (linux-image-generic) atlanır.</summary>
    internal static IReadOnlyList<KernelFact> ParseKernels(IEnumerable<string> lines)
    {
        var kernels = new Dictionary<string, KernelFact>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var parts = raw.Trim().Split('|');
            if (parts.Length != 3)
                continue;

            if (parts[1].StartsWith("linux-image-", StringComparison.Ordinal))
            {
                // dpkg: "install ok installed|linux-image-6.8.0-45-generic|123456"
                if (!parts[0].EndsWith("installed", StringComparison.Ordinal) || parts[0].Contains("not-installed", StringComparison.Ordinal))
                    continue;

                var version = parts[1]["linux-image-".Length..];
                if (version.Length == 0 || !char.IsDigit(version[0]))
                    continue;

                kernels[version] = new KernelFact(parts[1], version, ParseLong(parts[2]) * 1024);
            }
            else if (parts[0] is "kernel" or "kernel-core")
            {
                // rpm: "kernel-core|5.14.0-427.el9.x86_64|98765432"
                var version = parts[1];
                var size = ParseLong(parts[2]);
                if (!kernels.TryGetValue(version, out var existing) || (size ?? 0) > (existing.SizeBytes ?? 0))
                    kernels[version] = new KernelFact($"{parts[0]}-{version}", version, size);
            }
        }

        return kernels.Values.OrderBy(k => k.Version, StringComparer.Ordinal).ToList();
    }

    /// <summary>df -Pk çıktısından aygıt başına toplam boş alan (bayt); aynı aygıtın birden çok bağlama noktası bir kez sayılır.</summary>
    internal static long TotalAvailableBytes(string storageOutput)
    {
        var storage = ServerSystemParser.ParseStorage(storageOutput);
        return storage.FileSystems
            .GroupBy(f => f.Device, StringComparer.Ordinal)
            .Sum(g => g.First().AvailableKilobytes) * 1024;
    }

    internal static long? ParseUnitSize(string number, string unit)
    {
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        var multiplier = unit.ToUpperInvariant() switch
        {
            "" or "B" => 1d,
            "K" => 1024d,
            "M" => 1024d * 1024,
            "G" => 1024d * 1024 * 1024,
            "T" => 1024d * 1024 * 1024 * 1024,
            "P" => 1024d * 1024 * 1024 * 1024 * 1024,
            _ => 0d
        };
        return multiplier == 0 ? null : (long)Math.Round(value * multiplier);
    }

    private static long? ParseLong(string value) =>
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static DateTime? ParseUnixTime(string value) =>
        ParseLong(value) is { } seconds && seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;

    [GeneratedRegex(@"take up (?<number>\d+(?:\.\d+)?)\s*(?<unit>[KMGTP]?)B?\b", RegexOptions.IgnoreCase)]
    private static partial Regex JournalUsage();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex SnapName();

    [GeneratedRegex(@"^x?\d{1,10}$")]
    private static partial Regex SnapRevision();
}

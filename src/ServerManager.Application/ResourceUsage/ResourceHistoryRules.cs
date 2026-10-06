using System.Text.Json;

namespace ServerManager.Application.ResourceUsage;

/// <summary>Kaynak geçmişi için saf kurallar: anlık görüntüye girecek process'ler, zaman aralığı ve özetler.</summary>
public static class ResourceHistoryRules
{
    /// <summary>Anlık görüntüye CPU'ya göre ve belleğe göre ayrı ayrı alınan en üst process sayısı.</summary>
    public const int TopProcessCount = 10;

    /// <summary>Grafikte çizgi olarak gösterilen container sayısı.</summary>
    public const int ChartContainerCount = 5;

    /// <summary>Tabloda gösterilen satır sayısı.</summary>
    public const int SummaryRowCount = 15;

    /// <summary>Bu süreden uzun aralıklarda container grafiği saatlik özetlerden çizilir.</summary>
    public static readonly TimeSpan RawRangeLimit = TimeSpan.FromHours(48);

    /// <summary>Özel aralık için üst sınır.</summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(31);

    /// <summary>Process özeti için okunan en fazla anlık görüntü sayısı.</summary>
    public const int MaxSnapshotsForSummary = 3000;

    public const int MaxCommandLength = 300;

    private const int TargetPoints = 120;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly IReadOnlyList<(string Code, string Label, TimeSpan Duration)> Ranges =
    [
        ("1h", "Son 1 saat", TimeSpan.FromHours(1)),
        ("6h", "Son 6 saat", TimeSpan.FromHours(6)),
        ("24h", "Son 24 saat", TimeSpan.FromHours(24)),
        ("7d", "Son 7 gün", TimeSpan.FromDays(7))
    ];

    /// <summary>CPU'ya göre ilk N ile belleğe (RSS) göre ilk N'in birleşimi; CPU'ya göre sıralı.</summary>
    public static IReadOnlyList<ProcessSample> SelectTopProcesses(IEnumerable<ProcessSample> processes, int count = TopProcessCount)
    {
        var list = processes as IReadOnlyList<ProcessSample> ?? processes.ToList();
        var byCpu = list.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.ResidentKilobytes).Take(count);
        var byMemory = list.OrderByDescending(p => p.ResidentKilobytes).ThenByDescending(p => p.CpuPercent).Take(count);
        return byCpu.Concat(byMemory)
            .DistinctBy(p => p.Pid)
            .Select(p => p.Command.Length > MaxCommandLength ? p with { Command = p.Command[..MaxCommandLength] } : p)
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.ResidentKilobytes)
            .ToList();
    }

    public static string SerializeProcesses(IReadOnlyList<ProcessSample> processes) => JsonSerializer.Serialize(processes, JsonOptions);

    public static IReadOnlyList<ProcessSample> DeserializeProcesses(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ProcessSample>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Hazır aralık koduna (1h, 6h, 24h, 7d) veya özel başlangıç/bitişe göre UTC aralık. Geçersiz özel aralıkta son 24 saat;
    /// aralık <see cref="MaxRange"/> ile sınırlanır.
    /// </summary>
    public static (DateTime From, DateTime To) ResolveRange(string? code, DateTime? fromUtc, DateTime? toUtc, DateTime nowUtc)
    {
        if (fromUtc is { } from)
        {
            var to = toUtc is { } end && end > from ? end : nowUtc;
            if (to > nowUtc)
                to = nowUtc;
            if (from >= to)
                return (nowUtc - TimeSpan.FromHours(24), nowUtc);
            if (to - from > MaxRange)
                from = to - MaxRange;
            return (from, to);
        }

        var duration = Ranges.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase)).Duration;
        if (duration == TimeSpan.Zero)
            duration = TimeSpan.FromHours(24);
        return (nowUtc - duration, nowUtc);
    }

    public static bool UsesHourlyData(DateTime from, DateTime to) => to - from > RawRangeLimit;

    /// <summary>Grafikte ~120 nokta olacak kova süresi; toplama aralığından kısa olmaz.</summary>
    public static int BucketSeconds(DateTime from, DateTime to, int intervalMinutes)
    {
        var minimum = Math.Max(60, intervalMinutes * 60);
        var seconds = (int)Math.Ceiling((to - from).TotalSeconds / TargetPoints);
        return Math.Max(minimum, seconds);
    }

    /// <summary>
    /// Çalışan container'lar her zaman saklanır; çalışmayanlar yalnızca panelin bildiği (yönetilen servis) container'larsa
    /// ("Servis çalışmıyor" alarmı için) veya yeniden başlama sayısı sıfırdan büyükse. Böylece eski durmuş container'lar tabloyu doldurmaz.
    /// </summary>
    public static bool ShouldStore(ContainerStateFact container, IReadOnlySet<string> knownContainers) =>
        container.IsRunning
        || string.Equals(container.State, "restarting", StringComparison.OrdinalIgnoreCase)
        || container.RestartCount > 0
        || knownContainers.Contains(container.Name);

    /// <summary>Anlık görüntülerden process (komut adı) bazında özet; görünmediği anlar 0 sayılır.</summary>
    public static IReadOnlyList<ProcessUsageSummary> SummarizeProcesses(IReadOnlyList<ProcessSnapshotRow> snapshots)
    {
        if (snapshots.Count == 0)
            return [];

        var totals = new Dictionary<string, (double CpuSum, double CpuMax, long RssMax, int Seen, string? User)>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            // Aynı adı taşıyan process'ler (ör. birden çok php-fpm) o an için toplanır.
            foreach (var group in DeserializeProcesses(snapshot.ProcessesJson).GroupBy(p => p.Name, StringComparer.Ordinal))
            {
                var cpu = group.Sum(p => p.CpuPercent);
                var rss = group.Sum(p => p.ResidentKilobytes);
                var user = group.First().User;
                totals[group.Key] = totals.TryGetValue(group.Key, out var current)
                    ? (current.CpuSum + cpu, Math.Max(current.CpuMax, cpu), Math.Max(current.RssMax, rss), current.Seen + 1, current.User)
                    : (cpu, cpu, rss, 1, user);
            }
        }

        return totals
            .Select(pair => new ProcessUsageSummary(
                pair.Key,
                pair.Value.User,
                Math.Round(pair.Value.CpuSum / snapshots.Count, 1),
                Math.Round(pair.Value.CpuMax, 1),
                pair.Value.RssMax,
                pair.Value.Seen))
            .ToList();
    }

    /// <summary>Kovalara göre seriler; eksik kova null (grafikte boşluk).</summary>
    public static (IReadOnlyList<DateTime> Timestamps, IReadOnlyList<HistorySeries> Cpu, IReadOnlyList<HistorySeries> Memory) BuildSeries(
        IReadOnlyList<ContainerSeriesPoint> points, IReadOnlyList<string> cpuNames, IReadOnlyList<string> memoryNames)
    {
        var timestamps = points.Select(p => p.Bucket).Distinct().Order().ToList();
        var index = timestamps.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);
        var lookup = points.ToDictionary(p => (p.ContainerName, p.Bucket));

        IReadOnlyList<HistorySeries> Build(IReadOnlyList<string> names, Func<ContainerSeriesPoint, double> value) =>
            names.Select(name =>
            {
                var data = new double?[timestamps.Count];
                foreach (var t in timestamps)
                {
                    if (lookup.TryGetValue((name, t), out var point))
                        data[index[t]] = Math.Round(value(point), 2);
                }

                return new HistorySeries(name, data);
            }).ToList();

        return (timestamps, Build(cpuNames, p => p.CpuPercent), Build(memoryNames, p => p.MemoryBytes));
    }
}

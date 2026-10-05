using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

/// <summary>
/// CPU/RAM/disk kurallarını ham metriklerle değerlendirir. Süreli kurallarda son N dakikadaki tüm örneklerin eşiği
/// aşması ve örneklerin bu süreyi kapsaması gerekir; son örnek bayatsa sonuç bilinmez sayılır.
/// </summary>
public static class MetricRuleEvaluator
{
    public static (AlertConditionState State, double? Value) Evaluate(
        IReadOnlyList<MetricSample> samples,
        AlertRuleKind kind,
        double threshold,
        int durationMinutes,
        DateTime now,
        TimeSpan freshness,
        TimeSpan sampleInterval)
    {
        if (samples.Count == 0)
            return (AlertConditionState.Unknown, null);

        var ordered = samples.OrderBy(s => s.CollectedAt).ToList();
        var latest = ordered[^1];
        var latestValue = Read(latest, kind);
        if (now - latest.CollectedAt > freshness)
            return (AlertConditionState.Unknown, latestValue);

        if (latestValue <= threshold)
            return (AlertConditionState.Ok, latestValue);

        if (durationMinutes <= 0)
            return (AlertConditionState.Firing, latestValue);

        var duration = TimeSpan.FromMinutes(durationMinutes);
        var windowStart = now - duration;
        var window = ordered.Where(s => s.CollectedAt >= windowStart).ToList();

        // Son örnek tazelik sınırı içinde olsa da kural süresinden eskiyse pencere boş kalır; karar verilemez.
        if (window.Count == 0)
            return (AlertConditionState.Unknown, latestValue);

        if (window.Any(s => Read(s, kind) <= threshold))
            return (AlertConditionState.Ok, latestValue);

        // Pencerenin başı örneklenmemişse (izleme yeni başladı veya araya kesinti girdi) henüz karar verilmez.
        return IsCovered(window[0].CollectedAt, now, duration, sampleInterval)
            ? (AlertConditionState.Firing, window.Average(s => Read(s, kind)))
            : (AlertConditionState.Unknown, latestValue);
    }

    /// <summary>
    /// Uzun süreli kurallarda ham satırlar yerine veritabanında hesaplanan pencere özetiyle değerlendirir; sonuç
    /// <see cref="Evaluate"/> ile aynıdır. Son örnek <paramref name="recent"/>, pencere <paramref name="window"/> içinden okunur.
    /// </summary>
    public static (AlertConditionState State, double? Value) EvaluateWindow(
        IReadOnlyList<MetricSample> recent,
        MetricWindowStats? window,
        AlertRuleKind kind,
        double threshold,
        int durationMinutes,
        DateTime now,
        TimeSpan freshness,
        TimeSpan sampleInterval)
    {
        if (recent.Count == 0)
            return (AlertConditionState.Unknown, null);

        var latest = recent.MaxBy(s => s.CollectedAt)!;
        var latestValue = Read(latest, kind);
        if (now - latest.CollectedAt > freshness)
            return (AlertConditionState.Unknown, latestValue);

        if (latestValue <= threshold)
            return (AlertConditionState.Ok, latestValue);

        if (durationMinutes <= 0)
            return (AlertConditionState.Firing, latestValue);

        if (window is null || window.SampleCount == 0)
            return (AlertConditionState.Unknown, latestValue);

        if (window.Minimum(kind) <= threshold)
            return (AlertConditionState.Ok, latestValue);

        return IsCovered(window.FirstCollectedAt, now, TimeSpan.FromMinutes(durationMinutes), sampleInterval)
            ? (AlertConditionState.Firing, window.Average(kind))
            : (AlertConditionState.Unknown, latestValue);
    }

    public static double Read(MetricSample sample, AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.CpuUsage => sample.CpuPercent,
        AlertRuleKind.MemoryUsage => sample.MemoryPercent,
        AlertRuleKind.DiskUsage => sample.DiskPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Metrik kuralı değil.")
    };

    private static bool IsCovered(DateTime firstInWindow, DateTime now, TimeSpan duration, TimeSpan sampleInterval) =>
        now - firstInWindow >= duration - sampleInterval * 2;
}

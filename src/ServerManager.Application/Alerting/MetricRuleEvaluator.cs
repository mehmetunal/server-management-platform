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
        if (window.Any(s => Read(s, kind) <= threshold))
            return (AlertConditionState.Ok, latestValue);

        // Pencerenin başı örneklenmemişse (izleme yeni başladı veya araya kesinti girdi) henüz karar verilmez.
        var tolerance = sampleInterval * 2;
        var covered = now - window[0].CollectedAt >= duration - tolerance;
        return covered
            ? (AlertConditionState.Firing, window.Average(s => Read(s, kind)))
            : (AlertConditionState.Unknown, latestValue);
    }

    public static double Read(MetricSample sample, AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.CpuUsage => sample.CpuPercent,
        AlertRuleKind.MemoryUsage => sample.MemoryPercent,
        AlertRuleKind.DiskUsage => sample.DiskPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Metrik kuralı değil.")
    };
}

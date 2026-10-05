using ServerManager.Application.Alerting;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Alerting;

public class MetricRuleEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly Guid ServerId = Guid.NewGuid();

    private static MetricSample Cpu(double secondsAgo, double cpu) =>
        new(ServerId, Now.AddSeconds(-secondsAgo), cpu, 10, 20);

    private static List<MetricSample> Series(int minutes, double cpu) =>
        Enumerable.Range(0, minutes * 2 + 1).Select(i => Cpu(i * 30, cpu)).ToList();

    [Fact]
    public void No_samples_is_unknown()
    {
        var (state, value) = MetricRuleEvaluator.Evaluate([], AlertRuleKind.CpuUsage, 80, 5, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Unknown, state);
        Assert.Null(value);
    }

    [Fact]
    public void Stale_latest_sample_is_unknown()
    {
        var (state, _) = MetricRuleEvaluator.Evaluate([Cpu(600, 99)], AlertRuleKind.CpuUsage, 80, 0, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Unknown, state);
    }

    [Fact]
    public void Latest_below_threshold_is_ok()
    {
        var (state, value) = MetricRuleEvaluator.Evaluate([Cpu(10, 40)], AlertRuleKind.CpuUsage, 80, 5, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Ok, state);
        Assert.Equal(40, value);
    }

    [Fact]
    public void Without_duration_fires_on_latest_sample()
    {
        var (state, value) = MetricRuleEvaluator.Evaluate([Cpu(10, 95)], AlertRuleKind.CpuUsage, 80, 0, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Firing, state);
        Assert.Equal(95, value);
    }

    [Fact]
    public void Fires_when_whole_window_exceeds_threshold()
    {
        var (state, value) = MetricRuleEvaluator.Evaluate(Series(5, 90), AlertRuleKind.CpuUsage, 80, 5, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Firing, state);
        Assert.Equal(90, value);
    }

    [Fact]
    public void Single_dip_inside_window_keeps_ok()
    {
        var samples = Series(5, 90);
        samples[3] = Cpu(90, 50);

        var (state, _) = MetricRuleEvaluator.Evaluate(samples, AlertRuleKind.CpuUsage, 80, 5, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Ok, state);
    }

    [Fact]
    public void Window_not_yet_covered_is_unknown()
    {
        var (state, _) = MetricRuleEvaluator.Evaluate(Series(1, 95), AlertRuleKind.CpuUsage, 80, 5, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Unknown, state);
    }

    [Fact]
    public void Fresh_latest_sample_older_than_duration_is_unknown_instead_of_throwing()
    {
        // Tazelik 2 dk, kural süresi 1 dk: son örnek 90 sn önce; pencere boş kalır.
        var (state, value) = MetricRuleEvaluator.Evaluate([Cpu(90, 95), Cpu(120, 95)], AlertRuleKind.CpuUsage, 80, 1, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Unknown, state);
        Assert.Equal(95, value);
    }

    private static MetricWindowStats Window(double minCpu, double avgCpu, double firstMinutesAgo, int count = 100) =>
        new(ServerId, count, Now.AddMinutes(-firstMinutesAgo), minCpu, 10, 20, avgCpu, 10, 20);

    [Fact]
    public void Window_stats_fire_when_minimum_exceeds_threshold_and_window_is_covered()
    {
        var (state, value) = MetricRuleEvaluator.EvaluateWindow([Cpu(10, 95)], Window(85, 91, 120), AlertRuleKind.CpuUsage, 80, 120, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Firing, state);
        Assert.Equal(91, value);
    }

    [Fact]
    public void Window_stats_with_a_dip_is_ok()
    {
        var (state, _) = MetricRuleEvaluator.EvaluateWindow([Cpu(10, 95)], Window(40, 90, 120), AlertRuleKind.CpuUsage, 80, 120, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Ok, state);
    }

    [Fact]
    public void Window_stats_not_covered_or_missing_is_unknown()
    {
        var (partial, _) = MetricRuleEvaluator.EvaluateWindow([Cpu(10, 95)], Window(85, 90, 30), AlertRuleKind.CpuUsage, 80, 120, Now, Freshness, Interval);
        var (missing, _) = MetricRuleEvaluator.EvaluateWindow([Cpu(10, 95)], null, AlertRuleKind.CpuUsage, 80, 120, Now, Freshness, Interval);
        var (noRecent, value) = MetricRuleEvaluator.EvaluateWindow([], Window(85, 90, 120), AlertRuleKind.CpuUsage, 80, 120, Now, Freshness, Interval);

        Assert.Equal(AlertConditionState.Unknown, partial);
        Assert.Equal(AlertConditionState.Unknown, missing);
        Assert.Equal(AlertConditionState.Unknown, noRecent);
        Assert.Null(value);
    }

    [Fact]
    public void Window_stats_match_raw_evaluation()
    {
        var samples = Series(90, 92);
        var stats = new MetricWindowStats(ServerId, samples.Count, samples.Min(s => s.CollectedAt), samples.Min(s => s.CpuPercent), 10, 20,
            samples.Average(s => s.CpuPercent), 10, 20);

        var raw = MetricRuleEvaluator.Evaluate(samples, AlertRuleKind.CpuUsage, 80, 90, Now, Freshness, Interval);
        var aggregated = MetricRuleEvaluator.EvaluateWindow(samples, stats, AlertRuleKind.CpuUsage, 80, 90, Now, Freshness, Interval);

        Assert.Equal(raw, aggregated);
    }

    [Theory]
    [InlineData(AlertRuleKind.CpuUsage, 1)]
    [InlineData(AlertRuleKind.MemoryUsage, 2)]
    [InlineData(AlertRuleKind.DiskUsage, 3)]
    public void Reads_metric_by_kind(AlertRuleKind kind, double expected)
    {
        Assert.Equal(expected, MetricRuleEvaluator.Read(new MetricSample(ServerId, Now, 1, 2, 3), kind));
    }

    [Fact]
    public void Non_metric_kind_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricRuleEvaluator.Read(new MetricSample(ServerId, Now, 1, 2, 3), AlertRuleKind.ServerOffline));
    }
}

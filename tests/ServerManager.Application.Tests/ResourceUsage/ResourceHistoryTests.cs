using ServerManager.Application.Monitoring;
using ServerManager.Application.ResourceUsage;
using ServerManager.Application.Services;
using ServerManager.Infrastructure.Repositories;
using ServerManager.Infrastructure.ServerSystem;

namespace ServerManager.Application.Tests.ResourceUsage;

public class ResourceHistoryTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);

    // 2 çekirdek, CLK_TCK=100; /proc/stat toplamı 400 tick arttı => 2 saniye.
    private const string ProcessOutput = """
        @@rh:clk
        100
        @@rh:ncpu
        2
        @@rh:cpu0
        cpu  1000 0 500 8000 100 0 0 0 0 0
        @@rh:t0
        101 (postgres) S 1 101 101 0 -1 4194560 100 0 0 0 1000 200 0 0 20 0 1 0 100 1000 100
        202 (my (weird) app) R 1 202 202 0 -1 4194560 100 0 0 0 50 50 0 0 20 0 1 0 100 1000 100
        @@rh:cpu1
        cpu  1200 0 570 8130 100 0 0 0 0 0
        @@rh:t1
        101 (postgres) S 1 101 101 0 -1 4194560 100 0 0 0 1100 220 0 0 20 0 1 0 100 1000 100
        202 (my (weird) app) R 1 202 202 0 -1 4194560 100 0 0 0 60 50 0 0 20 0 1 0 100 1000 100
        303 (kworker/0:1) S 2 0 0 0 -1 69238880 0 0 0 0 0 0 0 0 20 0 1 0 100 0 0
        @@rh:ps
          101 postgres          5.0  12.5 524288 postgres: checkpointer
          202 www-data          1.0   0.4  20480 /usr/bin/node /srv/app/server.js --port 3000
        @@rh:end
        """;

    [Fact]
    public void Process_cpu_is_measured_from_proc_tick_deltas()
    {
        var (processes, busy) = ResourceHistoryParser.ParseProcesses(ProcessOutput);

        // Toplam 400 tick'in 270'i meşgul (idle+iowait 130 arttı).
        Assert.Equal(67.5, busy!.Value, 1);

        var postgres = processes.Single(p => p.Pid == 101);
        Assert.Equal(60, postgres.CpuPercent); // 120 tick / (100 * 2 sn)
        Assert.Equal("postgres", postgres.User);
        Assert.Equal(524288, postgres.ResidentKilobytes);
        Assert.Equal("postgres", postgres.Name);

        var node = processes.Single(p => p.Pid == 202);
        Assert.Equal(5, node.CpuPercent);
        Assert.Equal("node", node.Name);
        Assert.StartsWith("/usr/bin/node", node.Command);

        var kernel = processes.Single(p => p.Pid == 303);
        Assert.Equal("kworker/0:1", kernel.Name);
        Assert.Equal(0, kernel.CpuPercent);
    }

    [Fact]
    public void Without_proc_samples_ps_lifetime_cpu_is_used()
    {
        const string output = """
            @@rh:ps
              101 postgres          5.0  12.5 524288 postgres: checkpointer
            @@rh:end
            """;

        var (processes, busy) = ResourceHistoryParser.ParseProcesses(output);

        Assert.Null(busy);
        Assert.Equal(5, Assert.Single(processes).CpuPercent);
    }

    [Fact]
    public void Containers_join_inspect_state_with_stats()
    {
        const string output = """
            @@rh:inspect
            /sm-svc-db|0|running|healthy
            /api-web-1|7|restarting|
            /old-job|0|exited|
            @@rh:stats
            {"Name":"sm-svc-db","CPUPerc":"12.50%","MemUsage":"256MiB / 1GiB","MemPerc":"25.00%","NetIO":"1kB / 2kB","BlockIO":"3MB / 4MB","PIDs":"10"}
            @@rh:end
            """;

        Assert.True(ResourceHistoryParser.HasDocker(output));
        var containers = ResourceHistoryParser.ParseContainers(output);

        Assert.Equal(3, containers.Count);
        var db = containers.Single(c => c.Name == "sm-svc-db");
        Assert.True(db.IsRunning);
        Assert.Equal("healthy", db.Health);
        Assert.Equal(12.5, db.CpuPercent);
        Assert.Equal(256L * 1024 * 1024, db.MemoryUsageBytes);

        var api = containers.Single(c => c.Name == "api-web-1");
        Assert.Equal(("restarting", 7, (string?)null), (api.State, api.RestartCount, api.Health));
        Assert.Equal(0, api.CpuPercent);
    }

    [Fact]
    public void Missing_docker_is_detected()
    {
        Assert.False(ResourceHistoryParser.HasDocker("@@rh:nodocker\n@@rh:end\n"));
        Assert.True(ResourceHistoryParser.IsComplete("@@rh:nodocker\n@@rh:end\n"));
    }

    [Fact]
    public void Snapshot_keeps_union_of_top_cpu_and_top_memory()
    {
        var processes = Enumerable.Range(1, 30)
            .Select(i => new ProcessSample(i, "u", i, 0, 1000 * (31 - i), $"p{i}", $"/bin/p{i}"))
            .ToList();

        var top = ResourceHistoryRules.SelectTopProcesses(processes, 10);

        Assert.Equal(20, top.Count);
        Assert.Equal(30, top[0].Pid); // en yüksek CPU önce
        Assert.Contains(top, p => p.Pid == 1); // en yüksek bellek
        Assert.DoesNotContain(top, p => p.Pid == 15);
    }

    [Fact]
    public void Process_json_round_trips_with_short_names()
    {
        var json = ResourceHistoryRules.SerializeProcesses([new ProcessSample(7, "root", 12.5, 1.5, 2048, "nginx", "nginx: worker")]);

        Assert.Contains("\"p\":7", json);
        var back = Assert.Single(ResourceHistoryRules.DeserializeProcesses(json));
        Assert.Equal(("nginx", 12.5, 2048L), (back.Name, back.CpuPercent, back.ResidentKilobytes));
        Assert.Empty(ResourceHistoryRules.DeserializeProcesses("bozuk"));
    }

    [Fact]
    public void Process_summary_averages_over_all_snapshots_and_sums_same_names()
    {
        string Json(params ProcessSample[] rows) => ResourceHistoryRules.SerializeProcesses(rows);
        var snapshots = new[]
        {
            new ProcessSnapshotRow(Now, 50, Json(new ProcessSample(1, "www", 30, 1, 100, "php-fpm", "php-fpm"), new ProcessSample(2, "www", 20, 1, 200, "php-fpm", "php-fpm"))),
            new ProcessSnapshotRow(Now.AddMinutes(5), 10, Json(new ProcessSample(3, "pg", 10, 1, 900, "postgres", "postgres")))
        };

        var summary = ResourceHistoryRules.SummarizeProcesses(snapshots);

        var php = summary.Single(p => p.Name == "php-fpm");
        Assert.Equal((25.0, 50.0, 300L, 1), (php.CpuAvg, php.CpuMax, php.ResidentMaxKilobytes, php.Seen));
        Assert.Equal(5, summary.Single(p => p.Name == "postgres").CpuAvg);
    }

    [Theory]
    [InlineData("1h", 1)]
    [InlineData("6h", 6)]
    [InlineData("7d", 168)]
    [InlineData("bilinmeyen", 24)]
    [InlineData(null, 24)]
    public void Preset_ranges_resolve_relative_to_now(string? code, int hours)
    {
        var (from, to) = ResourceHistoryRules.ResolveRange(code, null, null, Now);

        Assert.Equal(Now, to);
        Assert.Equal(TimeSpan.FromHours(hours), to - from);
    }

    [Fact]
    public void Custom_range_is_clamped_to_now_and_max_span()
    {
        var (from, to) = ResourceHistoryRules.ResolveRange(null, Now.AddDays(-60), Now.AddDays(1), Now);

        Assert.Equal(Now, to);
        Assert.Equal(ResourceHistoryRules.MaxRange, to - from);

        var invalid = ResourceHistoryRules.ResolveRange(null, Now.AddHours(1), null, Now);
        Assert.Equal(TimeSpan.FromHours(24), invalid.To - invalid.From);
    }

    [Fact]
    public void Long_ranges_use_hourly_data_and_bucket_is_not_shorter_than_interval()
    {
        Assert.False(ResourceHistoryRules.UsesHourlyData(Now.AddHours(-24), Now));
        Assert.True(ResourceHistoryRules.UsesHourlyData(Now.AddDays(-7), Now));
        Assert.Equal(300, ResourceHistoryRules.BucketSeconds(Now.AddHours(-1), Now, 5));
        Assert.Equal(720, ResourceHistoryRules.BucketSeconds(Now.AddHours(-24), Now, 5));
    }

    [Fact]
    public void Only_running_restarting_or_known_containers_are_stored()
    {
        static ContainerStateFact Fact(string name, string state, int restarts = 0) =>
            new(name, state, null, restarts, 0, 0, 0, 0, 0, 0, 0, 0);
        var known = new HashSet<string>(["sm-svc-db"], StringComparer.Ordinal);
        var facts = new ResourceHistoryFacts
        {
            Containers = [Fact("web", "running"), Fact("sm-svc-db", "exited"), Fact("old", "exited"), Fact("crash", "exited", 4), Fact("loop", "restarting")]
        };

        var samples = ResourceHistoryService.BuildSamples(Guid.NewGuid(), Now, facts, known);

        Assert.Equal(["web", "sm-svc-db", "crash", "loop"], samples.Select(s => s.ContainerName));
    }

    [Fact]
    public void Series_fill_missing_buckets_with_null()
    {
        var points = new[]
        {
            new ContainerSeriesPoint(Now, "a", 10, 100),
            new ContainerSeriesPoint(Now.AddMinutes(5), "a", 20, 200),
            new ContainerSeriesPoint(Now.AddMinutes(5), "b", 5, 50)
        };

        var (timestamps, cpu, memory) = ResourceHistoryRules.BuildSeries(points, ["a", "b"], ["b"]);

        Assert.Equal(2, timestamps.Count);
        Assert.Equal([10, 20], cpu[0].Data);
        Assert.Equal([null, 5], cpu[1].Data);
        Assert.Equal("b", Assert.Single(memory).Name);
    }

    [Theory]
    [InlineData(RetentionTarget.ContainerMetrics, "ContainerMetricSamples", "CollectedAt")]
    [InlineData(RetentionTarget.ContainerMetricsHourly, "ContainerMetricsHourly", "HourStart")]
    [InlineData(RetentionTarget.ProcessSnapshots, "ProcessSnapshots", "CollectedAt")]
    public void History_tables_are_in_retention_allow_list(RetentionTarget target, string table, string column)
    {
        var mapping = RetentionAllowList.Resolve(target);

        Assert.Equal((table, column, "ServerId", RetentionMode.DeleteRows), (mapping.Table, mapping.TimeColumn, mapping.PartitionColumn, mapping.Mode));
        var create = RetentionDeleter.BuildKeepCreateSql(mapping);
        Assert.DoesNotContain("@", create);
        Assert.Contains($"FROM {table}", RetentionDeleter.BuildDeleteSql(mapping, protect: true));
    }

    [Fact]
    public void History_retention_defaults()
    {
        var options = new RetentionOptions();
        Assert.Equal((7, 90, 7), (options.ContainerMetricDays, options.ContainerMetricHourlyDays, options.ProcessSnapshotDays));
        Assert.Equal(5, new MonitoringOptions().ResourceHistoryIntervalMinutes);
        Assert.Equal(6, new MonitoringOptions().ReclaimableScanIntervalHours);
    }
}

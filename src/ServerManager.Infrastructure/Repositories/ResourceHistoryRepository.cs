using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.ResourceUsage;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ResourceHistoryRepository : IResourceHistoryRepository
{
    private const string BucketEpoch = "CAST('2020-01-01' AS datetime2)";

    /// <summary>Saatlik özet bu kadar geriye kadar tamamlanır (uygulama günlerce kapalı kaldıysa daha eskiler özetlenmez).</summary>
    private static readonly TimeSpan AggregateLookback = TimeSpan.FromDays(7);

    private readonly ApplicationDbContext _context;

    public ResourceHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Guid>> GetCollectableServerIdsAsync(CancellationToken cancellationToken = default) =>
        await CollectableServers()
            .OrderBy(s => s.Name)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetManagedContainerNamesAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        await _context.ManagedServices
            .AsNoTracking()
            .Where(s => s.ServerId == serverId && s.Status != ManagedServiceStatus.Removed)
            .Select(s => s.ContainerName)
            .ToListAsync(cancellationToken);

    public async Task AddSamplesAsync(IReadOnlyList<ContainerMetricSample> samples, CancellationToken cancellationToken = default) =>
        await _context.ContainerMetricSamples.AddRangeAsync(samples, cancellationToken);

    public async Task AddProcessSnapshotAsync(ProcessSnapshot snapshot, CancellationToken cancellationToken = default) =>
        await _context.ProcessSnapshots.AddAsync(snapshot, cancellationToken);

    public Task<int> AggregateHourlyAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            INSERT INTO ContainerMetricsHourly
                (ServerId, ContainerName, HourStart, SampleCount,
                 CpuPercentAvg, CpuPercentMax, MemoryUsageBytesAvg, MemoryUsageBytesMax, RestartCountMax)
            SELECT m.ServerId, m.ContainerName, h.HourStart, COUNT(*),
                   AVG(m.CpuPercent), MAX(m.CpuPercent), AVG(CAST(m.MemoryUsageBytes AS float)), MAX(m.MemoryUsageBytes), MAX(m.RestartCount)
            FROM ContainerMetricSamples m
            CROSS APPLY (SELECT DATEADD(HOUR, DATEDIFF(HOUR, {BucketEpoch}, m.CollectedAt), {BucketEpoch}) AS HourStart) h
            WHERE m.CollectedAt < @before AND m.CollectedAt >= @since
              AND NOT EXISTS (SELECT 1 FROM ContainerMetricsHourly x
                              WHERE x.ServerId = m.ServerId AND x.HourStart = h.HourStart AND x.ContainerName = m.ContainerName)
            GROUP BY m.ServerId, m.ContainerName, h.HourStart;
            """;

        return _context.Database.ExecuteSqlRawAsync(sql,
        [
            new SqlParameter("@before", SqlDbType.DateTime2) { Value = beforeUtc },
            new SqlParameter("@since", SqlDbType.DateTime2) { Value = beforeUtc - AggregateLookback }
        ], cancellationToken);
    }

    public async Task<IReadOnlyList<ContainerSeriesPoint>> GetSeriesAsync(
        Guid serverId, DateTime fromUtc, DateTime toUtc, int bucketSeconds, bool hourly, CancellationToken cancellationToken = default)
    {
        var sql = hourly
            ? """
              SELECT m.HourStart AS Bucket, m.ContainerName, m.CpuPercentAvg AS CpuPercent, m.MemoryUsageBytesAvg AS MemoryBytes
              FROM ContainerMetricsHourly m
              WHERE m.ServerId = @serverId AND m.HourStart >= @from AND m.HourStart <= @to
              """
            : $"""
              SELECT b.Bucket, m.ContainerName, AVG(m.CpuPercent) AS CpuPercent, AVG(CAST(m.MemoryUsageBytes AS float)) AS MemoryBytes
              FROM ContainerMetricSamples m
              CROSS APPLY (SELECT DATEADD(SECOND, (DATEDIFF(SECOND, {BucketEpoch}, m.CollectedAt) / @bucket) * @bucket, {BucketEpoch}) AS Bucket) b
              WHERE m.ServerId = @serverId AND m.CollectedAt >= @from AND m.CollectedAt <= @to
              GROUP BY b.Bucket, m.ContainerName
              """;

        var rows = await _context.Database
            .SqlQueryRaw<SeriesRow>(sql, RangeParameters(serverId, fromUtc, toUtc, bucketSeconds))
            .ToListAsync(cancellationToken);
        return rows.Select(r => new ContainerSeriesPoint(DateTime.SpecifyKind(r.Bucket, DateTimeKind.Utc), r.ContainerName, r.CpuPercent, r.MemoryBytes)).ToList();
    }

    public async Task<IReadOnlyList<ContainerUsageSummary>> GetContainerSummariesAsync(
        Guid serverId, DateTime fromUtc, DateTime toUtc, bool hourly, CancellationToken cancellationToken = default)
    {
        var sql = hourly
            ? """
              SELECT m.ContainerName AS Name, SUM(m.SampleCount) AS Samples,
                     SUM(m.CpuPercentAvg * m.SampleCount) / NULLIF(SUM(m.SampleCount), 0) AS CpuAvg, MAX(m.CpuPercentMax) AS CpuMax,
                     SUM(m.MemoryUsageBytesAvg * m.SampleCount) / NULLIF(SUM(m.SampleCount), 0) AS MemoryAvgBytes,
                     MAX(m.MemoryUsageBytesMax) AS MemoryMaxBytes,
                     CAST(NULL AS bigint) AS NetworkBytes, CAST(NULL AS bigint) AS BlockBytes,
                     MAX(m.RestartCountMax) - MIN(m.RestartCountMax) AS Restarts
              FROM ContainerMetricsHourly m
              WHERE m.ServerId = @serverId AND m.HourStart >= @from AND m.HourStart <= @to
              GROUP BY m.ContainerName
              """
            : """
              SELECT m.ContainerName AS Name, COUNT(*) AS Samples,
                     AVG(m.CpuPercent) AS CpuAvg, MAX(m.CpuPercent) AS CpuMax,
                     AVG(CAST(m.MemoryUsageBytes AS float)) AS MemoryAvgBytes, MAX(m.MemoryUsageBytes) AS MemoryMaxBytes,
                     MAX(m.NetworkRxBytes + m.NetworkTxBytes) - MIN(m.NetworkRxBytes + m.NetworkTxBytes) AS NetworkBytes,
                     MAX(m.BlockReadBytes + m.BlockWriteBytes) - MIN(m.BlockReadBytes + m.BlockWriteBytes) AS BlockBytes,
                     MAX(m.RestartCount) - MIN(m.RestartCount) AS Restarts
              FROM ContainerMetricSamples m
              WHERE m.ServerId = @serverId AND m.CollectedAt >= @from AND m.CollectedAt <= @to
              GROUP BY m.ContainerName
              """;

        var rows = await _context.Database
            .SqlQueryRaw<SummaryRow>(sql, RangeParameters(serverId, fromUtc, toUtc, 3600))
            .ToListAsync(cancellationToken);
        return rows.Select(r => new ContainerUsageSummary(
                r.Name,
                r.Samples,
                Math.Round(r.CpuAvg ?? 0, 2),
                Math.Round(r.CpuMax ?? 0, 2),
                Math.Round(r.MemoryAvgBytes ?? 0),
                r.MemoryMaxBytes ?? 0,
                r.NetworkBytes is { } network ? Math.Max(0, network) : null,
                r.BlockBytes is { } block ? Math.Max(0, block) : null,
                Math.Max(0, r.Restarts ?? 0)))
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessSnapshotRow>> GetProcessSnapshotsAsync(
        Guid serverId, DateTime fromUtc, DateTime toUtc, int max, CancellationToken cancellationToken = default)
    {
        var query = _context.ProcessSnapshots.AsNoTracking()
            .Where(p => p.ServerId == serverId && p.CollectedAt >= fromUtc && p.CollectedAt <= toUtc);
        var count = await query.CountAsync(cancellationToken);
        if (count == 0)
            return [];

        var step = Math.Max(1, (int)Math.Ceiling(count / (double)Math.Max(1, max)));
        const string sql = """
            WITH r AS (
                SELECT CollectedAt, CpuBusyPercent, ProcessesJson, ROW_NUMBER() OVER (ORDER BY CollectedAt) AS RowNumber
                FROM ProcessSnapshots
                WHERE ServerId = @serverId AND CollectedAt >= @from AND CollectedAt <= @to)
            SELECT CollectedAt, CpuBusyPercent, ProcessesJson FROM r
            WHERE (RowNumber - 1) % @bucket = 0
            ORDER BY CollectedAt
            """;

        var rows = await _context.Database
            .SqlQueryRaw<SnapshotRow>(sql, RangeParameters(serverId, fromUtc, toUtc, step))
            .ToListAsync(cancellationToken);
        return rows.Select(r => new ProcessSnapshotRow(DateTime.SpecifyKind(r.CollectedAt, DateTimeKind.Utc), r.CpuBusyPercent, r.ProcessesJson)).ToList();
    }

    public async Task<ProcessSnapshotRow?> GetNearestProcessSnapshotAsync(Guid serverId, DateTime atUtc, CancellationToken cancellationToken = default)
    {
        var snapshots = _context.ProcessSnapshots.AsNoTracking().Where(p => p.ServerId == serverId);
        var before = await snapshots.Where(p => p.CollectedAt <= atUtc).OrderByDescending(p => p.CollectedAt).FirstOrDefaultAsync(cancellationToken);
        var after = await snapshots.Where(p => p.CollectedAt > atUtc).OrderBy(p => p.CollectedAt).FirstOrDefaultAsync(cancellationToken);
        var nearest = (before, after) switch
        {
            (null, null) => null,
            ({ } b, null) => b,
            (null, { } a) => a,
            ({ } b, { } a) => atUtc - b.CollectedAt <= a.CollectedAt - atUtc ? b : a
        };

        return nearest is null
            ? null
            : new ProcessSnapshotRow(DateTime.SpecifyKind(nearest.CollectedAt, DateTimeKind.Utc), nearest.CpuBusyPercent, nearest.ProcessesJson);
    }

    public Task UpsertReclaimableAsync(ServerReclaimableSpace value, CancellationToken cancellationToken = default)
    {
        const string sql = """
            MERGE ServerReclaimableSpace WITH (HOLDLOCK) AS target
            USING (SELECT @serverId AS ServerId) AS source ON target.ServerId = source.ServerId
            WHEN MATCHED THEN UPDATE SET ScannedAt = @scannedAt, ReclaimableBytes = @bytes, SafeReclaimableBytes = @safe
            WHEN NOT MATCHED THEN INSERT (ServerId, ScannedAt, ReclaimableBytes, SafeReclaimableBytes)
                VALUES (@serverId, @scannedAt, @bytes, @safe);
            """;

        return _context.Database.ExecuteSqlRawAsync(sql,
        [
            new SqlParameter("@serverId", SqlDbType.UniqueIdentifier) { Value = value.ServerId },
            new SqlParameter("@scannedAt", SqlDbType.DateTime2) { Value = value.ScannedAt },
            new SqlParameter("@bytes", SqlDbType.BigInt) { Value = value.ReclaimableBytes },
            new SqlParameter("@safe", SqlDbType.BigInt) { Value = value.SafeReclaimableBytes }
        ], cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetReclaimableScanDueServerIdsAsync(DateTime scannedBefore, CancellationToken cancellationToken = default) =>
        await CollectableServers()
            .Where(s => !_context.ServerReclaimableSpace.Any(r => r.ServerId == s.Id && r.ScannedAt >= scannedBefore))
            .OrderBy(s => s.Name)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private IQueryable<Server> CollectableServers() =>
        _context.Servers
            .AsNoTracking()
            .Where(s => s.MonitoringEnabled && s.HostKeyFingerprint != null && s.Status != ServerStatus.Offline);

    private static object[] RangeParameters(Guid serverId, DateTime fromUtc, DateTime toUtc, int bucket) =>
    [
        new SqlParameter("@serverId", SqlDbType.UniqueIdentifier) { Value = serverId },
        new SqlParameter("@from", SqlDbType.DateTime2) { Value = fromUtc },
        new SqlParameter("@to", SqlDbType.DateTime2) { Value = toUtc },
        new SqlParameter("@bucket", SqlDbType.Int) { Value = Math.Max(1, bucket) }
    ];

    private sealed class SeriesRow
    {
        public DateTime Bucket { get; set; }

        public string ContainerName { get; set; } = string.Empty;

        public double CpuPercent { get; set; }

        public double MemoryBytes { get; set; }
    }

    private sealed class SummaryRow
    {
        public string Name { get; set; } = string.Empty;

        public int Samples { get; set; }

        public double? CpuAvg { get; set; }

        public double? CpuMax { get; set; }

        public double? MemoryAvgBytes { get; set; }

        public long? MemoryMaxBytes { get; set; }

        public long? NetworkBytes { get; set; }

        public long? BlockBytes { get; set; }

        public int? Restarts { get; set; }
    }

    private sealed class SnapshotRow
    {
        public DateTime CollectedAt { get; set; }

        public double? CpuBusyPercent { get; set; }

        public string ProcessesJson { get; set; } = "[]";
    }
}

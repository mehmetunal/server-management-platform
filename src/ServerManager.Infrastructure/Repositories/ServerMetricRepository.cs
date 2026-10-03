using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Persistence;

namespace ServerManager.Infrastructure.Repositories;

public class ServerMetricRepository : IServerMetricRepository
{
    private const int DeleteBatchSize = 5000;
    private const string BucketEpoch = "CAST('2020-01-01' AS datetime2)";

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServerMetricRepository> _logger;

    public ServerMetricRepository(ApplicationDbContext context, ILogger<ServerMetricRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddMetricAsync(ServerMetric metric, CancellationToken cancellationToken = default) =>
        await _context.ServerMetrics.AddAsync(metric, cancellationToken);

    public async Task AddHealthCheckAsync(ServerHealthCheck healthCheck, CancellationToken cancellationToken = default) =>
        await _context.ServerHealthChecks.AddAsync(healthCheck, cancellationToken);

    public async Task UpsertSnapshotAsync(Guid serverId, SystemMetricsSnapshot snapshot, DateTime collectedAt, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
        const string sql = """
            MERGE ServerMetricSnapshots WITH (HOLDLOCK) AS target
            USING (SELECT @serverId AS ServerId) AS source ON target.ServerId = source.ServerId
            WHEN MATCHED THEN UPDATE SET CollectedAt = @collectedAt, SnapshotJson = @json
            WHEN NOT MATCHED THEN INSERT (ServerId, CollectedAt, SnapshotJson) VALUES (@serverId, @collectedAt, @json);
            """;

        await _context.Database.ExecuteSqlRawAsync(sql,
        [
            new SqlParameter("@serverId", SqlDbType.UniqueIdentifier) { Value = serverId },
            new SqlParameter("@collectedAt", SqlDbType.DateTime2) { Value = collectedAt },
            new SqlParameter("@json", SqlDbType.NVarChar, -1) { Value = json }
        ], cancellationToken);
    }

    public async Task<(SystemMetricsSnapshot Snapshot, DateTime CollectedAt)?> GetSnapshotAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var row = await _context.ServerMetricSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServerId == serverId, cancellationToken);
        if (row is null)
            return null;

        try
        {
            var snapshot = JsonSerializer.Deserialize<SystemMetricsSnapshot>(row.SnapshotJson, SnapshotJsonOptions);
            return snapshot is null ? null : (snapshot, row.CollectedAt);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Metrik anlık görüntüsü okunamadı. ServerId: {ServerId}", serverId);
            return null;
        }
    }

    public Task<ServerMetric?> GetLatestAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        _context.ServerMetrics
            .AsNoTracking()
            .Where(m => m.ServerId == serverId)
            .OrderByDescending(m => m.CollectedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ServerMetric>> GetLatestForServersAsync(
        IReadOnlyCollection<Guid> serverIds, CancellationToken cancellationToken = default)
    {
        var ids = serverIds.Distinct().ToList();
        var latest = await _context.ServerMetrics
            .AsNoTracking()
            .Where(m => ids.Contains(m.ServerId))
            .GroupBy(m => m.ServerId)
            .Select(g => g.OrderByDescending(m => m.CollectedAt).First())
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(m => m.ServerId);
    }

    public Task<IReadOnlyList<MetricPointDto>> GetRawSeriesAsync(Guid? serverId, DateTime fromUtc, int bucketSeconds, CancellationToken cancellationToken = default) =>
        QuerySeriesAsync(
            "ServerMetrics", "CollectedAt",
            "CpuUsagePercent", "MemoryUsagePercent", "DiskUsagePercent",
            "NetworkRxBytesPerSecond", "NetworkTxBytesPerSecond", "LoadAverage1",
            serverId, fromUtc, bucketSeconds, cancellationToken);

    public Task<IReadOnlyList<MetricPointDto>> GetHourlySeriesAsync(Guid? serverId, DateTime fromUtc, int bucketSeconds, CancellationToken cancellationToken = default) =>
        QuerySeriesAsync(
            "ServerMetricsHourly", "HourStart",
            "CpuUsagePercentAvg", "MemoryUsagePercentAvg", "DiskUsagePercentAvg",
            "NetworkRxBytesPerSecondAvg", "NetworkTxBytesPerSecondAvg", "LoadAverage1Avg",
            serverId, fromUtc, bucketSeconds, cancellationToken);

    public async Task<IReadOnlyList<ServerHealthCheck>> GetRecentHealthChecksAsync(Guid serverId, int count, CancellationToken cancellationToken = default) =>
        await _context.ServerHealthChecks
            .AsNoTracking()
            .Where(h => h.ServerId == serverId)
            .OrderByDescending(h => h.CheckedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<double?> GetUptimePercentAsync(Guid serverId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var stats = await _context.ServerHealthChecks
            .AsNoTracking()
            .Where(h => h.ServerId == serverId && h.CheckedAt >= sinceUtc)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Success = g.Count(h => h.IsSuccess) })
            .FirstOrDefaultAsync(cancellationToken);

        return stats is null || stats.Total == 0 ? null : Math.Round(stats.Success * 100d / stats.Total, 2);
    }

    public async Task<FleetResourceSummaryDto> GetFleetSummaryAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var latest = await _context.ServerMetrics
            .AsNoTracking()
            .Where(m => m.CollectedAt >= sinceUtc && _context.Servers.Any(s => s.Id == m.ServerId && s.MonitoringEnabled))
            .GroupBy(m => m.ServerId)
            .Select(g => g.OrderByDescending(m => m.CollectedAt).First())
            .ToListAsync(cancellationToken);

        if (latest.Count == 0)
            return new FleetResourceSummaryDto();

        return new FleetResourceSummaryDto
        {
            ReportingServers = latest.Count,
            AverageCpuUsagePercent = Math.Round(latest.Average(m => m.CpuUsagePercent), 1),
            AverageMemoryUsagePercent = Math.Round(latest.Average(m => m.MemoryUsagePercent), 1),
            AverageDiskUsagePercent = Math.Round(latest.Average(m => m.DiskUsagePercent), 1),
            TotalNetworkRxBytesPerSecond = latest.Sum(m => m.NetworkRxBytesPerSecond),
            TotalNetworkTxBytesPerSecond = latest.Sum(m => m.NetworkTxBytesPerSecond)
        };
    }

    public Task<int> AggregateHourlyAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            INSERT INTO ServerMetricsHourly
                (ServerId, HourStart, SampleCount,
                 CpuUsagePercentAvg, CpuUsagePercentMax,
                 MemoryUsagePercentAvg, MemoryUsagePercentMax,
                 DiskUsagePercentAvg, DiskUsagePercentMax,
                 NetworkRxBytesPerSecondAvg, NetworkTxBytesPerSecondAvg, LoadAverage1Avg)
            SELECT m.ServerId, h.HourStart, COUNT(*),
                   AVG(m.CpuUsagePercent), MAX(m.CpuUsagePercent),
                   AVG(m.MemoryUsagePercent), MAX(m.MemoryUsagePercent),
                   AVG(m.DiskUsagePercent), MAX(m.DiskUsagePercent),
                   AVG(m.NetworkRxBytesPerSecond), AVG(m.NetworkTxBytesPerSecond), AVG(m.LoadAverage1)
            FROM ServerMetrics m
            CROSS APPLY (SELECT DATEADD(HOUR, DATEDIFF(HOUR, {BucketEpoch}, m.CollectedAt), {BucketEpoch}) AS HourStart) h
            WHERE m.CollectedAt < @before
              AND NOT EXISTS (SELECT 1 FROM ServerMetricsHourly x WHERE x.ServerId = m.ServerId AND x.HourStart = h.HourStart)
            GROUP BY m.ServerId, h.HourStart;
            """;

        return _context.Database.ExecuteSqlRawAsync(sql,
            [new SqlParameter("@before", SqlDbType.DateTime2) { Value = beforeUtc }],
            cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(RetentionTarget target, DateTime cutoffUtc, int keepLatestPerServer, CancellationToken cancellationToken = default)
    {
        var (table, timeColumn) = RetentionAllowList.Resolve(target);
        var keep = Math.Max(1, keepLatestPerServer);

        // Her sunucunun en yeni kayıtları süresi dolmuş olsa bile korunur.
        var sql = $"""
            DELETE TOP (@batch) t FROM {table} t
            WHERE t.{timeColumn} < @cutoff
              AND t.Id NOT IN (
                  SELECT r.Id FROM (
                      SELECT Id, ROW_NUMBER() OVER (PARTITION BY ServerId ORDER BY {timeColumn} DESC) AS RowNumber
                      FROM {table}) r
                  WHERE r.RowNumber <= @keep);
            """;

        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleted = await _context.Database.ExecuteSqlRawAsync(sql,
            [
                new SqlParameter("@batch", SqlDbType.Int) { Value = DeleteBatchSize },
                new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = cutoffUtc },
                new SqlParameter("@keep", SqlDbType.Int) { Value = keep }
            ], cancellationToken);

            total += deleted;
            if (deleted < DeleteBatchSize)
                break;
        }

        return total;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    private async Task<IReadOnlyList<MetricPointDto>> QuerySeriesAsync(
        string table, string timeColumn,
        string cpuColumn, string memoryColumn, string diskColumn,
        string rxColumn, string txColumn, string loadColumn,
        Guid? serverId, DateTime fromUtc, int bucketSeconds, CancellationToken cancellationToken)
    {
        var bucketExpression = $"DATEADD(SECOND, (DATEDIFF(SECOND, {BucketEpoch}, m.{timeColumn}) / @bucket) * @bucket, {BucketEpoch})";
        var serverFilter = serverId.HasValue ? "AND m.ServerId = @serverId" : string.Empty;

        // Önce sunucu bazında ortalama alınır; filo grafiğinde ağ trafiği sunucular arasında toplanır.
        var sql = $"""
            WITH PerServer AS (
                SELECT {bucketExpression} AS Bucket, m.ServerId,
                       AVG(m.{cpuColumn}) AS Cpu, AVG(m.{memoryColumn}) AS Memory, AVG(m.{diskColumn}) AS Disk,
                       AVG(m.{rxColumn}) AS Rx, AVG(m.{txColumn}) AS Tx, AVG(m.{loadColumn}) AS Load1
                FROM {table} m
                INNER JOIN Servers s ON s.Id = m.ServerId AND s.IsDeleted = 0
                WHERE m.{timeColumn} >= @from {serverFilter}
                GROUP BY {bucketExpression}, m.ServerId)
            SELECT Bucket AS [Timestamp],
                   AVG(Cpu) AS CpuUsagePercent, AVG(Memory) AS MemoryUsagePercent, AVG(Disk) AS DiskUsagePercent,
                   SUM(Rx) AS NetworkRxBytesPerSecond, SUM(Tx) AS NetworkTxBytesPerSecond, AVG(Load1) AS LoadAverage1
            FROM PerServer
            GROUP BY Bucket
            ORDER BY Bucket
            """;

        var parameters = new List<SqlParameter>
        {
            new("@bucket", SqlDbType.Int) { Value = Math.Max(1, bucketSeconds) },
            new("@from", SqlDbType.DateTime2) { Value = fromUtc }
        };
        if (serverId.HasValue)
            parameters.Add(new SqlParameter("@serverId", SqlDbType.UniqueIdentifier) { Value = serverId.Value });

        return await _context.Database
            .SqlQueryRaw<MetricPointDto>(sql, parameters.ToArray<object>())
            .ToListAsync(cancellationToken);
    }
}

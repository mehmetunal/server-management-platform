using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Monitoring;
using ServerManager.Application.ResourceUsage;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public sealed class ResourceHistoryService : IResourceHistoryService
{
    private const string NotFoundMessage = "Sunucu bulunamadı.";

    private readonly IResourceHistoryRepository _repository;
    private readonly IServerRepository _servers;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IResourceHistoryInspector _inspector;
    private readonly IServerCleanupService _cleanup;
    private readonly TimeProvider _timeProvider;
    private readonly MonitoringOptions _options;
    private readonly ILogger<ResourceHistoryService> _logger;

    public ResourceHistoryService(
        IResourceHistoryRepository repository,
        IServerRepository servers,
        IServerConnectionProvider connectionProvider,
        IResourceHistoryInspector inspector,
        IServerCleanupService cleanup,
        TimeProvider timeProvider,
        IOptions<MonitoringOptions> options,
        ILogger<ResourceHistoryService> logger)
    {
        _repository = repository;
        _servers = servers;
        _connectionProvider = connectionProvider;
        _inspector = inspector;
        _cleanup = cleanup;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public bool Enabled => _options.Enabled && _options.ResourceHistoryIntervalMinutes > 0;

    public int IntervalMinutes => Math.Max(1, _options.ResourceHistoryIntervalMinutes);

    public Task<IReadOnlyList<Guid>> GetCollectableServerIdsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetCollectableServerIdsAsync(cancellationToken);

    public async Task<ServiceResult<int>> CollectAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<int>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var facts = await _inspector.CollectAsync(connection.Data.Context, cancellationToken);
        if (!facts.IsSuccess || facts.Data is null)
            return ServiceResult<int>.Failure(facts.Message ?? "Kaynak geçmişi okunamadı.");

        var now = UtcNow;
        var known = (await _repository.GetManagedContainerNamesAsync(serverId, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var samples = BuildSamples(serverId, now, facts.Data, known);
        if (samples.Count > 0)
            await _repository.AddSamplesAsync(samples, cancellationToken);

        if (facts.Data.Processes.Count > 0 || facts.Data.CpuBusyPercent is not null)
        {
            await _repository.AddProcessSnapshotAsync(new ProcessSnapshot
            {
                ServerId = serverId,
                CollectedAt = now,
                CpuBusyPercent = facts.Data.CpuBusyPercent is { } busy ? Math.Round(busy, 1) : null,
                ProcessesJson = ResourceHistoryRules.SerializeProcesses(ResourceHistoryRules.SelectTopProcesses(facts.Data.Processes))
            }, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<int>.Success(samples.Count);
    }

    internal static List<ContainerMetricSample> BuildSamples(Guid serverId, DateTime now, ResourceHistoryFacts facts, IReadOnlySet<string> known) =>
        facts.Containers
            .Where(c => ResourceHistoryRules.ShouldStore(c, known))
            .Select(c => new ContainerMetricSample
            {
                ServerId = serverId,
                CollectedAt = now,
                ContainerName = TextHelper.Truncate(c.Name, 256)!,
                State = TextHelper.Truncate(c.State, 32)!,
                Health = string.IsNullOrWhiteSpace(c.Health) ? null : TextHelper.Truncate(c.Health, 32),
                RestartCount = c.RestartCount,
                CpuPercent = Math.Round(c.CpuPercent, 2),
                MemoryUsageBytes = c.MemoryUsageBytes,
                MemoryLimitBytes = c.MemoryLimitBytes,
                MemoryPercent = Math.Round(c.MemoryPercent, 2),
                NetworkRxBytes = c.NetworkRxBytes,
                NetworkTxBytes = c.NetworkTxBytes,
                BlockReadBytes = c.BlockReadBytes,
                BlockWriteBytes = c.BlockWriteBytes
            })
            .ToList();

    public Task<int> AggregateHourlyAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        return _repository.AggregateHourlyAsync(new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc), cancellationToken);
    }

    public async Task<ServiceResult<ResourceHistoryReport>> GetReportAsync(
        Guid serverId, string? range, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        if (!await _servers.AnyAsync(s => s.Id == serverId, cancellationToken))
            return ServiceResult<ResourceHistoryReport>.NotFound(NotFoundMessage);

        var (from, to) = ResourceHistoryRules.ResolveRange(range, fromUtc, toUtc, UtcNow);
        var hourly = ResourceHistoryRules.UsesHourlyData(from, to);
        var bucket = hourly ? 3600 : ResourceHistoryRules.BucketSeconds(from, to, IntervalMinutes);

        var containers = await _repository.GetContainerSummariesAsync(serverId, from, to, hourly, cancellationToken);
        var cpuNames = containers.OrderByDescending(c => c.CpuAvg).Take(ResourceHistoryRules.ChartContainerCount).Select(c => c.Name).ToList();
        var memoryNames = containers.OrderByDescending(c => c.MemoryAvgBytes).Take(ResourceHistoryRules.ChartContainerCount).Select(c => c.Name).ToList();

        var points = containers.Count == 0 ? [] : await _repository.GetSeriesAsync(serverId, from, to, bucket, hourly, cancellationToken);
        var wanted = cpuNames.Concat(memoryNames).ToHashSet(StringComparer.Ordinal);
        var (timestamps, cpu, memory) = ResourceHistoryRules.BuildSeries(points.Where(p => wanted.Contains(p.ContainerName)).ToList(), cpuNames, memoryNames);

        var snapshots = await _repository.GetProcessSnapshotsAsync(serverId, from, to, ResourceHistoryRules.MaxSnapshotsForSummary, cancellationToken);
        var processes = ResourceHistoryRules.SummarizeProcesses(snapshots);

        return ServiceResult<ResourceHistoryReport>.Success(new ResourceHistoryReport
        {
            From = from,
            To = to,
            Source = hourly ? "hourly" : "raw",
            Timestamps = timestamps,
            Cpu = cpu,
            Memory = memory,
            Containers = containers.OrderByDescending(c => c.CpuAvg).ThenByDescending(c => c.MemoryAvgBytes).Take(ResourceHistoryRules.SummaryRowCount).ToList(),
            ProcessesByCpu = processes.OrderByDescending(p => p.CpuAvg).ThenByDescending(p => p.CpuMax).Take(ResourceHistoryRules.TopProcessCount).ToList(),
            ProcessesByMemory = processes.OrderByDescending(p => p.ResidentMaxKilobytes).Take(ResourceHistoryRules.TopProcessCount).ToList(),
            SnapshotCount = snapshots.Count
        });
    }

    public async Task<ServiceResult<ProcessSnapshotView>> GetProcessSnapshotAsync(Guid serverId, DateTime atUtc, CancellationToken cancellationToken = default)
    {
        if (!await _servers.AnyAsync(s => s.Id == serverId, cancellationToken))
            return ServiceResult<ProcessSnapshotView>.NotFound(NotFoundMessage);

        var row = await _repository.GetNearestProcessSnapshotAsync(serverId, atUtc, cancellationToken);
        if (row is null)
            return ServiceResult<ProcessSnapshotView>.NotFound("Bu zamana yakın process kaydı yok.");

        return ServiceResult<ProcessSnapshotView>.Success(new ProcessSnapshotView
        {
            CollectedAt = row.CollectedAt,
            CpuBusyPercent = row.CpuBusyPercent,
            Processes = ResourceHistoryRules.DeserializeProcesses(row.ProcessesJson)
        });
    }

    public Task<IReadOnlyList<Guid>> GetReclaimableScanDueServerIdsAsync(CancellationToken cancellationToken = default) =>
        _options.ReclaimableScanIntervalHours <= 0
            ? Task.FromResult<IReadOnlyList<Guid>>([])
            : _repository.GetReclaimableScanDueServerIdsAsync(UtcNow.AddHours(-_options.ReclaimableScanIntervalHours), cancellationToken);

    public async Task<ServiceResult<long>> ScanReclaimableAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var scan = await _cleanup.ScanAsync(serverId, new CleanupOptions(), cancellationToken);
        if (!scan.IsSuccess || scan.Data is null)
        {
            _logger.LogWarning("Temizlenebilir alan taraması yapılamadı. ServerId: {ServerId}, Reason: {Reason}", serverId, scan.Message);
            return ServiceResult<long>.Failure(scan.Message ?? "Sunucu taranamadı.", scan.ErrorType);
        }

        await _repository.UpsertReclaimableAsync(new ServerReclaimableSpace
        {
            ServerId = serverId,
            ScannedAt = UtcNow,
            ReclaimableBytes = scan.Data.ReclaimableBytes,
            SafeReclaimableBytes = scan.Data.SafeReclaimableBytes
        }, cancellationToken);
        return ServiceResult<long>.Success(scan.Data.ReclaimableBytes);
    }
}

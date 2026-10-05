using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Agent;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Mappings;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class MonitoringService : IMonitoringService
{
    public const int RecentHealthCheckCount = 20;
    public const int ProtectedHealthChecksPerServer = 50;
    public const string SystemUserName = "Sistem";

    private const string NotFoundMessage = "Sunucu bulunamadı.";

    private readonly IServerRepository _serverRepository;
    private readonly IServerMetricRepository _metricRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IMetricsCollector _metricsCollector;
    private readonly IMonitoringNotifier _notifier;
    private readonly IAuditLogService _auditLogService;
    private readonly TimeProvider _timeProvider;
    private readonly MonitoringOptions _options;
    private readonly RetentionOptions _retention;
    private readonly ILogger<MonitoringService> _logger;

    public MonitoringService(
        IServerRepository serverRepository,
        IServerMetricRepository metricRepository,
        ISecretProtector secretProtector,
        IMetricsCollector metricsCollector,
        IMonitoringNotifier notifier,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        IOptions<MonitoringOptions> options,
        IOptions<RetentionOptions> retention,
        ILogger<MonitoringService> logger)
    {
        _serverRepository = serverRepository;
        _metricRepository = metricRepository;
        _secretProtector = secretProtector;
        _metricsCollector = metricsCollector;
        _notifier = notifier;
        _auditLogService = auditLogService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _retention = retention.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<Guid>> GetCollectableServerIdsAsync(CancellationToken cancellationToken = default) =>
        _serverRepository.GetMonitorableIdsAsync(_timeProvider.GetUtcNow().UtcDateTime - AgentRules.ActiveWindow, cancellationToken);

    public async Task<ServiceResult<ServerMonitoringUpdateDto>> CollectAsync(Guid serverId, bool manual, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetWithDetailsAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<ServerMonitoringUpdateDto>.NotFound(NotFoundMessage);

        if (!manual && !server.MonitoringEnabled)
            return ServiceResult<ServerMonitoringUpdateDto>.Failure("Bu sunucu için izleme kapalı.");

        if (server.HostKeyFingerprint is null)
            return ServiceResult<ServerMonitoringUpdateDto>.Failure("Host key henüz doğrulanmadı. Önce \"Bağlantıyı Test Et\" ile sunucuyu doğrulayın.");

        if (server.Credential is null)
            return ServiceResult<ServerMonitoringUpdateDto>.Failure("Bu sunucu için kayıtlı kimlik bilgisi bulunamadı.");

        MetricsCollectionResult result;
        try
        {
            var request = new SshConnectionRequest
            {
                Host = server.IpAddress,
                Port = server.SshPort,
                Username = server.Username,
                AuthenticationType = server.AuthenticationType,
                Password = Decrypt(server.Credential.EncryptedPassword),
                PrivateKey = Decrypt(server.Credential.EncryptedPrivateKey),
                Passphrase = Decrypt(server.Credential.EncryptedPassphrase),
                ExpectedHostKeyFingerprint = server.HostKeyFingerprint
            };
            result = await _metricsCollector.CollectAsync(request, cancellationToken);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Sunucu kimlik bilgileri çözülemedi. ServerId: {ServerId}", server.Id);
            result = new MetricsCollectionResult { IsSuccess = false, Message = "Kimlik bilgileri çözülemedi. Master key değişmiş olabilir." };
        }

        return await ApplyResultAsync(server, result, manual, cancellationToken);
    }

    public async Task<ServiceResult<ServerMonitoringUpdateDto>> RecordAgentReportAsync(Guid serverId, MetricsCollectionResult result, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        return server is null
            ? ServiceResult<ServerMonitoringUpdateDto>.NotFound(NotFoundMessage)
            : await ApplyResultAsync(server, result, manual: false, cancellationToken);
    }

    private async Task<ServiceResult<ServerMonitoringUpdateDto>> ApplyResultAsync(Server server, MetricsCollectionResult result, bool manual, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var previousStatus = server.Status;
        var statusReasons = new List<string>();
        ServerResourceSummaryDto? latest = null;

        await _metricRepository.AddHealthCheckAsync(new ServerHealthCheck
        {
            ServerId = server.Id,
            CheckedAt = now,
            IsSuccess = result.IsSuccess,
            ResponseTimeMs = result.DurationMs,
            Message = TextHelper.Truncate(result.Message, 500)
        }, cancellationToken);

        if (result is { IsSuccess: true, Snapshot: not null })
        {
            var snapshot = result.Snapshot;
            var metric = snapshot.ToMetric(server.Id, now);
            await _metricRepository.AddMetricAsync(metric, cancellationToken);
            await _metricRepository.UpsertSnapshotAsync(server.Id, snapshot, now, cancellationToken);

            server.LastSeenAt = now;
            server.ConsecutiveFailureCount = 0;

            if (string.IsNullOrWhiteSpace(server.OperatingSystem) && !string.IsNullOrWhiteSpace(snapshot.System.OperatingSystem))
                server.OperatingSystem = TextHelper.Truncate(snapshot.System.OperatingSystem, 128);

            var evaluation = ServerStatusEvaluator.Evaluate(snapshot, _options);
            statusReasons.AddRange(evaluation.Reasons);
            if (server.Status != ServerStatus.Maintenance)
                server.Status = evaluation.Status;

            latest = metric.ToSummaryDto(server.Status);
        }
        else
        {
            server.ConsecutiveFailureCount++;
            statusReasons.Add(result.Message);
            if (server.Status != ServerStatus.Maintenance
                && (result.FingerprintMismatch || server.ConsecutiveFailureCount >= Math.Max(1, _options.OfflineAfterFailures)))
                server.Status = ServerStatus.Offline;
        }

        await _serverRepository.SaveChangesAsync(cancellationToken);

        var update = new ServerMonitoringUpdateDto
        {
            ServerId = server.Id,
            ServerName = server.Name,
            Status = server.Status,
            PreviousStatus = previousStatus,
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            CheckedAt = now,
            Latest = latest
        };

        if (update.StatusChanged)
        {
            _logger.LogInformation(
                "Sunucu durumu değişti. ServerId: {ServerId}, {PreviousStatus} -> {Status}",
                server.Id, previousStatus, server.Status);

            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.ServerStatusChanged,
                AuditEntityTypes.Server,
                server.Id.ToString(),
                server.Name,
                BuildStatusChangeDetails(previousStatus, server.Status, statusReasons),
                server.Status is not (ServerStatus.Critical or ServerStatus.Offline),
                manual ? null : SystemUserName), cancellationToken);
        }

        if (manual)
        {
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.ServerMetricsCollect,
                AuditEntityTypes.Server,
                server.Id.ToString(),
                server.Name,
                result.Message,
                result.IsSuccess), cancellationToken);
        }

        await NotifySafelyAsync(update, cancellationToken);
        return ServiceResult<ServerMonitoringUpdateDto>.Success(update, result.Message);
    }

    public async Task<ServiceResult<ServerMonitoringOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.FirstOrDefaultAsync(s => s.Id == serverId, cancellationToken);
        if (server is null)
            return ServiceResult<ServerMonitoringOverviewDto>.NotFound(NotFoundMessage);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await _metricRepository.GetSnapshotAsync(serverId, cancellationToken);
        var latest = await _metricRepository.GetLatestAsync(serverId, cancellationToken);
        var healthChecks = await _metricRepository.GetRecentHealthChecksAsync(serverId, RecentHealthCheckCount, cancellationToken);
        var uptime = await _metricRepository.GetUptimePercentAsync(serverId, now.AddHours(-24), cancellationToken);

        return ServiceResult<ServerMonitoringOverviewDto>.Success(new ServerMonitoringOverviewDto
        {
            ServerId = serverId,
            MonitoringEnabled = server.MonitoringEnabled,
            HasTrustedHostKey = server.HostKeyFingerprint is not null,
            LastSeenAt = server.LastSeenAt,
            Snapshot = snapshot?.Snapshot,
            SnapshotCollectedAt = snapshot?.CollectedAt,
            Latest = latest?.ToSummaryDto(server.Status),
            UptimePercent24h = uptime,
            RecentHealthChecks = healthChecks.Select(h => h.ToDto()).ToList()
        });
    }

    public async Task<ServiceResult<IReadOnlyList<MetricPointDto>>> GetSeriesAsync(Guid serverId, MetricRange range, CancellationToken cancellationToken = default)
    {
        if (!await _serverRepository.AnyAsync(s => s.Id == serverId, cancellationToken))
            return ServiceResult<IReadOnlyList<MetricPointDto>>.NotFound(NotFoundMessage);

        var points = await GetSeriesCoreAsync(serverId, range, cancellationToken);
        return ServiceResult<IReadOnlyList<MetricPointDto>>.Success(points);
    }

    public Task<IReadOnlyList<MetricPointDto>> GetFleetSeriesAsync(MetricRange range, CancellationToken cancellationToken = default) =>
        GetSeriesCoreAsync(null, range, cancellationToken);

    public Task<FleetResourceSummaryDto> GetFleetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return _metricRepository.GetFleetSummaryAsync(now - FreshnessWindow, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ServerResourceSummaryDto>> GetLatestSummariesAsync(
        IReadOnlyCollection<Guid> serverIds, CancellationToken cancellationToken = default)
    {
        if (serverIds.Count == 0)
            return new Dictionary<Guid, ServerResourceSummaryDto>();

        var metrics = await _metricRepository.GetLatestForServersAsync(serverIds, cancellationToken);
        return metrics.ToDictionary(pair => pair.Key, pair => pair.Value.ToSummaryDto(null));
    }

    public async Task<MaintenanceResult> RunMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var currentHourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        var aggregated = await _metricRepository.AggregateHourlyAsync(currentHourStart, cancellationToken);
        var deletedRaw = await _metricRepository.DeleteExpiredAsync(
            RetentionTarget.RawMetrics, now.AddHours(-_options.EffectiveRawRetentionHours), 1, cancellationToken);
        var deletedHourly = await _metricRepository.DeleteExpiredAsync(
            RetentionTarget.HourlyMetrics, now.AddDays(-Math.Max(1, _options.HourlyRetentionDays)), 1, cancellationToken);
        var deletedHealthChecks = await _metricRepository.DeleteExpiredAsync(
            RetentionTarget.HealthChecks, now.AddDays(-Math.Max(1, _options.HealthCheckRetentionDays)), ProtectedHealthChecksPerServer, cancellationToken);

        var history = new Dictionary<RetentionTarget, int>();
        foreach (var (target, days, keep) in HistoryTargets())
        {
            if (RetentionOptions.Cutoff(days, now) is not { } cutoff)
                continue;

            // Bir geçmiş tablosunun temizliği başarısız olsa da diğerleri ve metrik bakımı etkilenmez.
            try
            {
                history[target] = await _metricRepository.DeleteExpiredAsync(target, cutoff, keep, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Geçmiş kayıt temizliği başarısız. Target: {Target}", target);
            }
        }

        var result = new MaintenanceResult(aggregated, deletedRaw, deletedHourly, deletedHealthChecks, history);
        _logger.LogInformation(
            "Metrik bakımı tamamlandı. Aggregated: {Aggregated}, DeletedRaw: {DeletedRaw}, DeletedHourly: {DeletedHourly}, DeletedHealthChecks: {DeletedHealthChecks}",
            result.AggregatedHours, result.DeletedRawMetrics, result.DeletedHourlyMetrics, result.DeletedHealthChecks);
        if (history.Values.Any(count => count > 0))
        {
            _logger.LogInformation("Geçmiş kayıtlar temizlendi. {Counts}",
                string.Join(", ", history.Where(pair => pair.Value > 0).Select(pair => $"{pair.Key}: {pair.Value}")));
        }

        return result;
    }

    /// <summary>Panelden saklama süresi verilen geçmiş/log hedefleri ve bölüm başına korunan en yeni kayıt sayısı.</summary>
    private IEnumerable<(RetentionTarget Target, int Days, int KeepLatest)> HistoryTargets()
    {
        yield return (RetentionTarget.DeploymentLogs, _retention.DeploymentLogDays, RetentionOptions.ProtectedDeploymentLogsPerProject);
        yield return (RetentionTarget.BackupRunLogs, _retention.BackupRunLogDays, RetentionOptions.ProtectedBackupRunLogsPerJob);
        yield return (RetentionTarget.CommandRuns, _retention.CommandRunDays, 1);
        yield return (RetentionTarget.TerminalSessions, _retention.TerminalSessionDays, 1);
        yield return (RetentionTarget.AlertEvents, _retention.AlertEventDays, 1);
        yield return (RetentionTarget.ServiceOperationLogs, _retention.ServiceOperationLogDays, RetentionOptions.ProtectedServiceOperationLogsPerService);
    }

    private TimeSpan FreshnessWindow => TimeSpan.FromSeconds(Math.Max(120, _options.IntervalSeconds * 4));

    private Task<IReadOnlyList<MetricPointDto>> GetSeriesCoreAsync(Guid? serverId, MetricRange range, CancellationToken cancellationToken)
    {
        var from = _timeProvider.GetUtcNow().UtcDateTime - MetricRanges.Duration(range);
        var bucketSeconds = Math.Max(MetricRanges.BucketSeconds(range), _options.IntervalSeconds);
        return MetricRanges.UsesHourlyData(range)
            ? _metricRepository.GetHourlySeriesAsync(serverId, from, bucketSeconds, cancellationToken)
            : _metricRepository.GetRawSeriesAsync(serverId, from, bucketSeconds, cancellationToken);
    }

    private static string BuildStatusChangeDetails(ServerStatus previous, ServerStatus current, IReadOnlyList<string> reasons)
    {
        var text = $"{previous} -> {current}";
        return reasons.Count == 0 ? text : $"{text}: {string.Join(", ", reasons)}";
    }

    private async Task NotifySafelyAsync(ServerMonitoringUpdateDto update, CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.ServerUpdatedAsync(update, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "İzleme bildirimi gönderilemedi. ServerId: {ServerId}", update.ServerId);
        }
    }

    private string? Decrypt(string? value) => value is null ? null : _secretProtector.Unprotect(value);
}

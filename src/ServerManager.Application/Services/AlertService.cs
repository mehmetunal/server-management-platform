using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class AlertService : IAlertService
{
    public const int SummaryLatestCount = 5;
    public const int ProtectedDeliveriesPerChannel = 50;
    public const string RuleInactiveMessage = "Kural devre dışı bırakıldı veya silindi; alarm kapatıldı.";

    private const string NotFoundMessage = "Alarm bulunamadı.";

    private readonly IAlertRepository _repository;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly AlertingOptions _options;
    private readonly MonitoringOptions _monitoringOptions;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        IAlertRepository repository,
        INotificationDispatcher dispatcher,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        IOptions<AlertingOptions> options,
        IOptions<MonitoringOptions> monitoringOptions,
        ILogger<AlertService> logger)
    {
        _repository = repository;
        _dispatcher = dispatcher;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _options = options.Value;
        _monitoringOptions = monitoringOptions.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<AlertEventDto>> SearchAsync(AlertEventFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _repository.SearchEventsAsync(filter, cancellationToken);
        return page.Map(ToDto);
    }

    public async Task<AlertSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var (firing, critical) = await _repository.CountFiringAsync(cancellationToken);
        var latest = firing == 0 ? [] : await _repository.GetLatestFiringAsync(SummaryLatestCount, cancellationToken);
        return new AlertSummaryDto(firing, critical, latest.Select(ToDto).ToList());
    }

    public async Task<ServiceResult> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await _repository.GetEventAsync(id, cancellationToken);
        if (alert is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (alert.Status != AlertEventStatus.Firing)
            return ServiceResult.Failure("Alarm zaten kapanmış.", ServiceErrorType.Conflict);

        if (alert.AcknowledgedAt is not null)
            return ServiceResult.Success($"Alarm {alert.AcknowledgedBy} tarafından zaten üstlenilmiş.");

        alert.AcknowledgedAt = UtcNow;
        alert.AcknowledgedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.AlertAcknowledge, AuditEntityTypes.AlertEvent, alert.Id.ToString(), $"{alert.TargetName}: {alert.RuleName}"), cancellationToken);
        return ServiceResult.Success("Alarm üstlenildi; kapanana kadar hatırlatma gönderilmez.");
    }

    public async Task<AlertEvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var rules = await _repository.GetEnabledRulesAsync(cancellationToken);
        var openEvents = await _repository.GetOpenEventsAsync(cancellationToken);
        var data = new AlertEvaluationData(_repository, rules, now, Freshness, cancellationToken);

        var notifications = new List<(AlertEvent Event, AlertRule Rule, NotificationKind Kind)>();
        int opened = 0, recovered = 0, closed = 0, reminders = 0;
        var activeRuleIds = rules.Select(r => r.Id).ToHashSet();

        foreach (var rule in rules)
        {
            // Bir kuralın verisi okunamazsa (beklenmeyen veri, sorgu hatası) diğer kurallar yine değerlendirilir.
            AlertReconcileResult result;
            try
            {
                var conditions = await BuildConditionsAsync(rule, data, now);
                var ruleEvents = openEvents.Where(e => e.RuleId == rule.Id).ToList();
                result = AlertReconciler.Reconcile(rule, conditions, ruleEvents, now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Alarm kuralı değerlendirilemedi. RuleId: {RuleId}, Kind: {Kind}", rule.Id, rule.Kind);
                continue;
            }

            foreach (var alert in result.Opened)
            {
                await _repository.AddEventAsync(alert, cancellationToken);
                notifications.Add((alert, rule, NotificationKind.Firing));
            }

            notifications.AddRange(result.PendingNotifications.Select(e => (e, rule, NotificationKind.Firing)));
            if (rule.NotifyRecovery)
                notifications.AddRange(result.Recovered.Select(e => (e, rule, NotificationKind.Recovery)));
            notifications.AddRange(result.Reminders.Select(e => (e, rule, NotificationKind.Reminder)));

            opened += result.Opened.Count;
            recovered += result.Recovered.Count;
            closed += result.Closed.Count;
            reminders += result.Reminders.Count;
        }

        foreach (var orphan in openEvents.Where(e => !activeRuleIds.Contains(e.RuleId)))
        {
            AlertReconciler.Resolve(orphan, now, RuleInactiveMessage);
            closed++;
        }

        // Yeni alarmlar bildirim zamanı boş (bekliyor) olarak kaydedilir; gönderim kesilirse sonraki turda yeniden denenir.
        await _repository.SaveChangesAsync(cancellationToken);

        int deliveries = 0, failed = 0;
        foreach (var (alert, rule, kind) in notifications)
        {
            try
            {
                var (sent, errors) = await NotifyAsync(alert, rule, kind, now, cancellationToken);
                deliveries += sent;
                failed += errors;

                // Her gönderimin sonucu hemen kaydedilir; sonraki bir hata önceki bildirimlerin tekrar gönderilmesine yol açmaz.
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Alarm bildirimi gönderilemedi. AlertId: {AlertId}, Kind: {Kind}", alert.Id, kind);
            }
        }

        var summary = new AlertEvaluationResult(rules.Count, opened, recovered, closed, reminders, deliveries, failed);
        if (opened + recovered + closed + reminders > 0)
        {
            _logger.LogInformation(
                "Alarm değerlendirmesi. Rules: {Rules}, Opened: {Opened}, Recovered: {Recovered}, Closed: {Closed}, Reminders: {Reminders}, Deliveries: {Deliveries}, Failed: {Failed}",
                summary.Rules, summary.Opened, summary.Recovered, summary.Closed, summary.Reminders, summary.Deliveries, summary.FailedDeliveries);
        }

        return summary;
    }

    public async Task<int> RunMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = UtcNow.AddDays(-Math.Max(7, _options.DeliveryRetentionDays));
        var deleted = await _repository.DeleteExpiredDeliveriesAsync(cutoff, ProtectedDeliveriesPerChannel, cancellationToken);
        if (deleted > 0)
            _logger.LogInformation("Bildirim gönderim kayıtları temizlendi. Deleted: {Deleted}", deleted);
        return deleted;
    }

    private TimeSpan Freshness => TimeSpan.FromSeconds(Math.Max(120, _monitoringOptions.IntervalSeconds * 4));

    /// <summary>Container örneği bu süreden eskiyse durum bilinmiyor sayılır (kaynak geçmişi aralığının 3 katı, en az 15 dk).</summary>
    private TimeSpan ContainerFreshness => TimeSpan.FromMinutes(Math.Max(15, Math.Max(1, _monitoringOptions.ResourceHistoryIntervalMinutes) * 3));

    public async Task<IReadOnlyList<AlertEventDto>> GetOpenServiceAlertsAsync(Guid serviceId, Guid serverId, string containerName, CancellationToken cancellationToken = default)
    {
        var events = await _repository.GetOpenServiceEventsAsync(serviceId, AlertRuleKinds.ContainerTargetKey(serverId, containerName), cancellationToken);
        return events.Select(ToDto).ToList();
    }

    private TimeSpan SampleInterval => TimeSpan.FromSeconds(Math.Max(10, _monitoringOptions.IntervalSeconds));

    private async Task<IReadOnlyList<AlertCondition>> BuildConditionsAsync(AlertRule rule, AlertEvaluationData data, DateTime now) => rule.Kind switch
    {
        AlertRuleKind.CpuUsage or AlertRuleKind.MemoryUsage or AlertRuleKind.DiskUsage =>
            AlertConditionEvaluator.ForMetrics(rule, await data.ServersAsync(), await data.SamplesAsync(), now, Freshness, SampleInterval,
                AlertEvaluationData.UsesWindowStats(rule) ? await data.WindowStatsAsync(rule.DurationMinutes) : null),
        AlertRuleKind.ServerOffline => AlertConditionEvaluator.ForServerOffline(rule, await data.ServersAsync(), now),
        AlertRuleKind.UptimeCheckDown => AlertConditionEvaluator.ForUptime(rule, await data.UptimeAsync(), now),
        AlertRuleKind.SslCertificateExpiry => AlertConditionEvaluator.ForSsl(rule, await data.SslAsync(), now),
        AlertRuleKind.DeploymentFailed => AlertConditionEvaluator.ForDeployments(rule, await data.DeploymentsAsync()),
        AlertRuleKind.BackupFailed => AlertConditionEvaluator.ForBackups(rule, await data.BackupsAsync()),
        AlertRuleKind.SecurityFinding => AlertConditionEvaluator.ForSecurity(rule, await data.SecurityAsync()),
        AlertRuleKind.ServiceDown => AlertConditionEvaluator.ForServiceDown(
            rule, await data.ServicesAsync(), await data.ContainerSamplesAsync(ContainerFreshness), now, ContainerFreshness),
        AlertRuleKind.ContainerRestartLoop => AlertConditionEvaluator.ForRestartLoop(
            rule, await data.ServersAsync(), await data.ServicesAsync(), await data.ContainerSamplesAsync(ContainerFreshness), now, ContainerFreshness),
        AlertRuleKind.ReclaimableSpace => AlertConditionEvaluator.ForReclaimable(rule, await data.ReclaimableAsync()),
        _ => []
    };

    /// <summary>
    /// Bildirimi kanallara gönderir. Açılış ve hatırlatmada bildirim zamanı yalnızca en az bir kanal başarılıysa
    /// (veya kuralın uygun kanalı yoksa) işaretlenir; tüm kanallar başarısızsa sonraki turda yeniden denenir.
    /// </summary>
    private async Task<(int Sent, int Failed)> NotifyAsync(AlertEvent alert, AlertRule rule, NotificationKind kind, DateTime now, CancellationToken cancellationToken)
    {
        var channels = rule.Channels
            .Select(link => link.Channel)
            .Where(channel => channel is { IsEnabled: true, IsDeleted: false } && alert.Severity >= channel.MinimumSeverity)
            .Select(channel => channel!)
            .ToList();

        int sent = 0, failed = 0;
        if (channels.Count > 0)
        {
            var message = AlertMessageBuilder.Build(alert, kind, now, _options.PublicBaseUrl);
            foreach (var channel in channels)
            {
                var result = await _dispatcher.SendAsync(channel, message, alert.Id, cancellationToken);
                if (result.IsSuccess) sent++;
                else failed++;
            }
        }

        if (kind != NotificationKind.Recovery && ShouldMarkNotified(sent, failed))
        {
            alert.LastNotifiedAt = now;
            alert.NotifiedValue = alert.Value;
        }

        return (sent, failed);
    }

    public static bool ShouldMarkNotified(int sent, int failed) => sent > 0 || failed == 0;

    private static AlertEventDto ToDto(AlertEvent e) => new(
        e.Id, e.RuleId, e.RuleName, e.Kind, e.Severity, e.ServerId, e.ServerName, e.TargetName, e.Status,
        e.Message, e.StartedAt, e.ResolvedAt, e.ResolvedMessage, e.AcknowledgedAt, e.AcknowledgedBy);
}

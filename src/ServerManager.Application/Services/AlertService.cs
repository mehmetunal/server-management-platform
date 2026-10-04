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
            var conditions = await BuildConditionsAsync(rule, data, now);
            var ruleEvents = openEvents.Where(e => e.RuleId == rule.Id).ToList();
            var result = AlertReconciler.Reconcile(rule, conditions, ruleEvents, now);

            foreach (var alert in result.Opened)
            {
                await _repository.AddEventAsync(alert, cancellationToken);
                notifications.Add((alert, rule, NotificationKind.Firing));
            }

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

        await _repository.SaveChangesAsync(cancellationToken);

        int deliveries = 0, failed = 0;
        foreach (var (alert, rule, kind) in notifications)
        {
            var (sent, errors) = await NotifyAsync(alert, rule, kind, now, cancellationToken);
            deliveries += sent;
            failed += errors;
        }

        if (notifications.Count > 0)
            await _repository.SaveChangesAsync(cancellationToken);

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

    private TimeSpan SampleInterval => TimeSpan.FromSeconds(Math.Max(10, _monitoringOptions.IntervalSeconds));

    private async Task<IReadOnlyList<AlertCondition>> BuildConditionsAsync(AlertRule rule, AlertEvaluationData data, DateTime now) => rule.Kind switch
    {
        AlertRuleKind.CpuUsage or AlertRuleKind.MemoryUsage or AlertRuleKind.DiskUsage =>
            AlertConditionEvaluator.ForMetrics(rule, await data.ServersAsync(), await data.SamplesAsync(), now, Freshness, SampleInterval),
        AlertRuleKind.ServerOffline => AlertConditionEvaluator.ForServerOffline(rule, await data.ServersAsync(), now),
        AlertRuleKind.UptimeCheckDown => AlertConditionEvaluator.ForUptime(rule, await data.UptimeAsync(), now),
        AlertRuleKind.SslCertificateExpiry => AlertConditionEvaluator.ForSsl(rule, await data.SslAsync(), now),
        AlertRuleKind.DeploymentFailed => AlertConditionEvaluator.ForDeployments(rule, await data.DeploymentsAsync()),
        AlertRuleKind.BackupFailed => AlertConditionEvaluator.ForBackups(rule, await data.BackupsAsync()),
        _ => []
    };

    private async Task<(int Sent, int Failed)> NotifyAsync(AlertEvent alert, AlertRule rule, NotificationKind kind, DateTime now, CancellationToken cancellationToken)
    {
        if (kind != NotificationKind.Recovery)
        {
            alert.LastNotifiedAt = now;
            alert.NotifiedValue = alert.Value;
        }

        var channels = rule.Channels
            .Select(link => link.Channel)
            .Where(channel => channel is { IsEnabled: true, IsDeleted: false } && alert.Severity >= channel.MinimumSeverity)
            .Select(channel => channel!)
            .ToList();
        if (channels.Count == 0)
            return (0, 0);

        var message = AlertMessageBuilder.Build(alert, kind, now, _options.PublicBaseUrl);
        int sent = 0, failed = 0;
        foreach (var channel in channels)
        {
            var result = await _dispatcher.SendAsync(channel, message, alert.Id, cancellationToken);
            if (result.IsSuccess) sent++;
            else failed++;
        }

        return (sent, failed);
    }

    private static AlertEventDto ToDto(AlertEvent e) => new(
        e.Id, e.RuleId, e.RuleName, e.Kind, e.Severity, e.ServerId, e.ServerName, e.TargetName, e.Status,
        e.Message, e.StartedAt, e.ResolvedAt, e.ResolvedMessage, e.AcknowledgedAt, e.AcknowledgedBy);
}

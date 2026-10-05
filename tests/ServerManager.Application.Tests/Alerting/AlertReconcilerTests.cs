using ServerManager.Application.Alerting;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Alerting;

public class AlertReconcilerTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private static AlertRule Rule(AlertRuleKind kind = AlertRuleKind.CpuUsage, int repeatMinutes = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Yüksek CPU",
        Kind = kind,
        Severity = AlertSeverity.Critical,
        RepeatIntervalMinutes = repeatMinutes
    };

    private static AlertCondition Condition(string key, AlertConditionState state, double? value = 90) =>
        new(key, $"hedef-{key}", null, "web-01", state, value, $"durum {state}");

    private static AlertEvent Open(AlertRule rule, string key, DateTime? lastNotified = null, double? notifiedValue = null) => new()
    {
        RuleId = rule.Id,
        RuleName = rule.Name,
        Kind = rule.Kind,
        Severity = rule.Severity,
        TargetKey = key,
        TargetName = $"hedef-{key}",
        Message = "eski",
        StartedAt = Now.AddHours(-1),
        LastNotifiedAt = lastNotified,
        NotifiedValue = notifiedValue
    };

    [Fact]
    public void Opens_event_for_new_firing_target()
    {
        var rule = Rule();

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing)], [], Now);

        var opened = Assert.Single(result.Opened);
        Assert.Equal("a", opened.TargetKey);
        Assert.Equal(AlertSeverity.Critical, opened.Severity);
        Assert.Equal(Now, opened.StartedAt);
        Assert.Equal(AlertEventStatus.Firing, opened.Status);
    }

    [Fact]
    public void Unknown_state_neither_opens_nor_closes()
    {
        var rule = Rule();
        var open = Open(rule, "a");

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Unknown), Condition("b", AlertConditionState.Unknown)], [open], Now);

        Assert.Empty(result.Opened);
        Assert.Empty(result.Recovered);
        Assert.Empty(result.Closed);
        Assert.Equal(AlertEventStatus.Firing, open.Status);
    }

    [Fact]
    public void Single_ok_evaluation_keeps_event_open()
    {
        var rule = Rule();
        var open = Open(rule, "a");

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Ok)], [open], Now);

        Assert.Empty(result.Recovered);
        Assert.Equal(AlertEventStatus.Firing, open.Status);
        Assert.Equal(1, open.ConsecutiveOkCount);
    }

    [Fact]
    public void Firing_between_ok_evaluations_resets_recovery_counter()
    {
        var rule = Rule();
        var open = Open(rule, "a");

        AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Ok)], [open], Now);
        AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing)], [open], Now.AddMinutes(1));
        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Ok)], [open], Now.AddMinutes(2));

        Assert.Empty(result.Recovered);
        Assert.Equal(AlertEventStatus.Firing, open.Status);
        Assert.Equal(1, open.ConsecutiveOkCount);
    }

    [Fact]
    public void Recovers_event_after_consecutive_ok_evaluations()
    {
        var rule = Rule();
        var open = Open(rule, "a");

        AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Ok)], [open], Now.AddMinutes(-1));
        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Ok)], [open], Now);

        Assert.Same(open, Assert.Single(result.Recovered));
        Assert.Equal(AlertEventStatus.Resolved, open.Status);
        Assert.Equal(Now, open.ResolvedAt);
        Assert.Equal("durum Ok", open.ResolvedMessage);
    }

    [Fact]
    public void Closes_without_recovery_when_target_disappears()
    {
        var rule = Rule();
        var open = Open(rule, "gone");

        var result = AlertReconciler.Reconcile(rule, [], [open], Now);

        Assert.Same(open, Assert.Single(result.Closed));
        Assert.Empty(result.Recovered);
        Assert.Equal(AlertReconciler.TargetGoneMessage, open.ResolvedMessage);
    }

    [Fact]
    public void Duplicate_open_events_keep_only_newest()
    {
        var rule = Rule();
        var older = Open(rule, "a");
        var newer = Open(rule, "a");
        newer.StartedAt = Now.AddMinutes(-5);

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing)], [older, newer], Now);

        Assert.Same(older, Assert.Single(result.Closed));
        Assert.Equal(AlertEventStatus.Firing, newer.Status);
        Assert.Empty(result.Opened);
    }

    [Fact]
    public void Updates_value_of_ongoing_event()
    {
        var rule = Rule();
        var open = Open(rule, "a");

        AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing, 97)], [open], Now);

        Assert.Equal(97, open.Value);
        Assert.Equal("durum Firing", open.Message);
        Assert.Equal("web-01", open.ServerName);
    }

    [Fact]
    public void Reminds_after_repeat_interval()
    {
        var rule = Rule(repeatMinutes: 30);
        var due = Open(rule, "a", lastNotified: Now.AddMinutes(-31));
        var recent = Open(rule, "b", lastNotified: Now.AddMinutes(-10));

        var result = AlertReconciler.Reconcile(rule,
            [Condition("a", AlertConditionState.Firing), Condition("b", AlertConditionState.Firing)], [due, recent], Now);

        Assert.Same(due, Assert.Single(result.Reminders));
    }

    [Fact]
    public void Acknowledged_event_is_not_reminded()
    {
        var rule = Rule(repeatMinutes: 30);
        var open = Open(rule, "a", lastNotified: Now.AddHours(-2));
        open.AcknowledgedAt = Now.AddHours(-1);

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing)], [open], Now);

        Assert.Empty(result.Reminders);
    }

    [Fact]
    public void Never_notified_event_is_retried_as_pending_not_reminded()
    {
        var rule = Rule(repeatMinutes: 1);
        var open = Open(rule, "a");

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing)], [open], Now);

        Assert.Empty(result.Reminders);
        Assert.Same(open, Assert.Single(result.PendingNotifications));
    }

    [Fact]
    public void Acknowledged_never_notified_event_is_not_pending()
    {
        var rule = Rule();
        var open = Open(rule, "a");
        open.AcknowledgedAt = Now.AddMinutes(-1);

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing)], [open], Now);

        Assert.Empty(result.PendingNotifications);
    }

    [Fact]
    public void Ssl_rule_reminds_when_crossing_expiry_step()
    {
        var rule = Rule(AlertRuleKind.SslCertificateExpiry);
        var open = Open(rule, "a", lastNotified: Now.AddDays(-1), notifiedValue: 16);

        var result = AlertReconciler.Reconcile(rule, [Condition("a", AlertConditionState.Firing, 14)], [open], Now);

        Assert.Same(open, Assert.Single(result.Reminders));
    }
}

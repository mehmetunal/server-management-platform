using ServerManager.Application.Common;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

/// <summary>
/// Bir kuralın güncel koşullarını açık alarmlarıyla karşılaştırır: yeni alarm açar, düzelenleri kapatır,
/// süren alarmlar için hatırlatma kararını verir. Her hedef için en fazla bir açık alarm bulunur.
/// </summary>
public static class AlertReconciler
{
    public const string TargetGoneMessage = "Hedef artık izlenmiyor; alarm kapatıldı.";

    /// <summary>Açık alarmın kapanması için gereken art arda "düzeldi" değerlendirme sayısı (eşik çevresinde dalgalanmayı önler).</summary>
    public const int RequiredOkEvaluations = 2;

    public static AlertReconcileResult Reconcile(AlertRule rule, IReadOnlyList<AlertCondition> conditions, IReadOnlyList<AlertEvent> openEvents, DateTime now)
    {
        var result = new AlertReconcileResult();
        var byTarget = conditions
            .GroupBy(c => c.TargetKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var openByTarget = new Dictionary<string, AlertEvent>(StringComparer.Ordinal);

        foreach (var open in openEvents.OrderByDescending(e => e.StartedAt))
        {
            if (!openByTarget.TryAdd(open.TargetKey, open))
            {
                Resolve(open, now, TargetGoneMessage);
                result.Closed.Add(open);
            }
        }

        foreach (var (targetKey, open) in openByTarget)
        {
            if (!byTarget.TryGetValue(targetKey, out var condition))
            {
                Resolve(open, now, TargetGoneMessage);
                result.Closed.Add(open);
                continue;
            }

            switch (condition.State)
            {
                case AlertConditionState.Ok:
                    open.ConsecutiveOkCount++;
                    if (open.ConsecutiveOkCount >= RequiredOkEvaluations)
                    {
                        Resolve(open, now, condition.Message);
                        result.Recovered.Add(open);
                    }
                    break;
                case AlertConditionState.Firing:
                    open.ConsecutiveOkCount = 0;
                    open.Value = condition.Value;
                    open.Message = TextHelper.Truncate(condition.Message, 1000)!;
                    if (open.ServerName is null && condition.ServerName is not null)
                        open.ServerName = TextHelper.Truncate(condition.ServerName, 256);
                    if (open.LastNotifiedAt is null)
                    {
                        // Açılış bildirimi gönderilemediyse (kanal hatası, kayıt öncesi kesinti) yeniden denenir; üstlenilmişse gerek yok.
                        if (open.AcknowledgedAt is null)
                            result.PendingNotifications.Add(open);
                    }
                    else if (ShouldRemind(rule, open, condition, now))
                    {
                        result.Reminders.Add(open);
                    }
                    break;
            }
        }

        foreach (var condition in byTarget.Values.Where(c => c.State == AlertConditionState.Firing && !openByTarget.ContainsKey(c.TargetKey)))
        {
            result.Opened.Add(new AlertEvent
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Kind = rule.Kind,
                Severity = rule.Severity,
                ServerId = condition.ServerId,
                ServerName = TextHelper.Truncate(condition.ServerName, 256),
                TargetKey = TextHelper.Truncate(condition.TargetKey, 100)!,
                TargetName = TextHelper.Truncate(condition.TargetName, 256)!,
                Status = AlertEventStatus.Firing,
                Message = TextHelper.Truncate(condition.Message, 1000)!,
                Value = condition.Value,
                StartedAt = now
            });
        }

        return result;
    }

    public static void Resolve(AlertEvent alert, DateTime now, string message)
    {
        alert.Status = AlertEventStatus.Resolved;
        alert.ResolvedAt = now;
        alert.ResolvedMessage = TextHelper.Truncate(message, 1000);
    }

    private static bool ShouldRemind(AlertRule rule, AlertEvent open, AlertCondition condition, DateTime now)
    {
        if (open.LastNotifiedAt is null || open.AcknowledgedAt is not null)
            return false;

        if (rule.Kind == AlertRuleKind.SslCertificateExpiry && condition.Value is { } days && SslExpirySteps.ShouldRemind(open.NotifiedValue, days))
            return true;

        return rule.RepeatIntervalMinutes > 0 && now - open.LastNotifiedAt.Value >= TimeSpan.FromMinutes(rule.RepeatIntervalMinutes);
    }
}

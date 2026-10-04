using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

/// <summary>Kural türüne göre her hedef için <see cref="AlertCondition"/> üretir; veritabanına dokunmaz.</summary>
public static class AlertConditionEvaluator
{
    public static IReadOnlyList<AlertCondition> ForMetrics(
        AlertRule rule,
        IReadOnlyList<AlertServerSnapshot> servers,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetricSample>> samples,
        DateTime now,
        TimeSpan freshness,
        TimeSpan sampleInterval)
    {
        var name = AlertRuleKinds.DisplayName(rule.Kind);
        return Scope(rule, servers).Select(server =>
        {
            if (server.Status == ServerStatus.Maintenance)
                return Condition(server, AlertConditionState.Unknown, null, "Sunucu bakım modunda.");

            var serverSamples = samples.GetValueOrDefault(server.Id) ?? [];
            var (state, value) = MetricRuleEvaluator.Evaluate(serverSamples, rule.Kind, rule.Threshold, rule.DurationMinutes, now, freshness, sampleInterval);
            var current = value is null ? "bilinmiyor" : AlertRuleKinds.Percent(value.Value);
            var message = state == AlertConditionState.Firing
                ? $"{name} {current} (eşik {AlertRuleKinds.Percent(rule.Threshold)}{DurationText(rule.DurationMinutes)})."
                : $"{name} {current}; eşiğin ({AlertRuleKinds.Percent(rule.Threshold)}) altında.";
            return Condition(server, state, value, message);
        }).ToList();
    }

    public static IReadOnlyList<AlertCondition> ForServerOffline(AlertRule rule, IReadOnlyList<AlertServerSnapshot> servers, DateTime now) =>
        Scope(rule, servers).Select(server =>
        {
            if (server.Status == ServerStatus.Maintenance)
                return Condition(server, AlertConditionState.Unknown, null, "Sunucu bakım modunda.");

            if (server.Status != ServerStatus.Offline)
                return Condition(server, server.Status == ServerStatus.Unknown ? AlertConditionState.Unknown : AlertConditionState.Ok, null, "Sunucuya yeniden erişiliyor.");

            var since = server.LastSeenAt ?? server.CreatedAt;
            var minutes = Math.Max(0, (now - since).TotalMinutes);
            var state = minutes >= rule.DurationMinutes ? AlertConditionState.Firing : AlertConditionState.Unknown;
            var message = server.LastSeenAt is null
                ? "Sunucuya hiç erişilemedi."
                : $"Sunucuya {Math.Floor(minutes)} dakikadır erişilemiyor (son erişim {AlertRuleKinds.Date(server.LastSeenAt.Value)} {server.LastSeenAt.Value:HH:mm} UTC).";
            return Condition(server, state, Math.Floor(minutes), message);
        }).ToList();

    public static IReadOnlyList<AlertCondition> ForUptime(AlertRule rule, IReadOnlyList<AlertUptimeSnapshot> checks, DateTime now) =>
        checks
            .Where(check => rule.ServerId is null || check.ServerId == rule.ServerId)
            .Select(check =>
            {
                var key = check.Id.ToString();
                if (check.Status == UptimeStatus.Up)
                    return new AlertCondition(key, check.Name, check.ServerId, check.ServerName, AlertConditionState.Ok, 0, $"{check.Name} yeniden yanıt veriyor.");

                if (check.Status != UptimeStatus.Down)
                    return new AlertCondition(key, check.Name, check.ServerId, check.ServerName, AlertConditionState.Unknown, null, "Henüz kontrol edilmedi.");

                var downFor = check.StatusChangedAt is { } changed ? now - changed : TimeSpan.Zero;
                var state = downFor.TotalMinutes >= rule.DurationMinutes ? AlertConditionState.Firing : AlertConditionState.Unknown;
                var reason = string.IsNullOrWhiteSpace(check.LastError) ? "yanıt alınamadı" : check.LastError;
                return new AlertCondition(key, check.Name, check.ServerId, check.ServerName, state, check.ConsecutiveFailures,
                    $"{check.Name} yanıt vermiyor: {reason} ({check.ConsecutiveFailures} ardışık hata).");
            }).ToList();

    public static IReadOnlyList<AlertCondition> ForSsl(AlertRule rule, IReadOnlyList<AlertSslSnapshot> monitors, DateTime now) =>
        monitors
            .Where(monitor => rule.ServerId is null || monitor.ServerId == rule.ServerId)
            .Select(monitor =>
            {
                var key = monitor.Id.ToString();
                var name = $"{monitor.Host}:{monitor.Port}";
                if (monitor.NotAfter is not { } notAfter)
                    return new AlertCondition(key, name, monitor.ServerId, monitor.ServerName, AlertConditionState.Unknown, null, "Sertifika henüz okunmadı.");

                var days = Math.Floor((notAfter - now).TotalDays);
                if (days >= rule.Threshold)
                    return new AlertCondition(key, name, monitor.ServerId, monitor.ServerName, AlertConditionState.Ok, days,
                        $"Sertifika yenilendi; bitiş {AlertRuleKinds.Date(notAfter)} ({AlertRuleKinds.Number(days)} gün).");

                var message = notAfter <= now
                    ? $"{name} sertifikasının süresi {AlertRuleKinds.Date(notAfter)} tarihinde doldu."
                    : $"{name} sertifikasının bitmesine {AlertRuleKinds.Number(days)} gün kaldı ({AlertRuleKinds.Date(notAfter)}).";
                return new AlertCondition(key, name, monitor.ServerId, monitor.ServerName, AlertConditionState.Firing, days, message);
            }).ToList();

    public static IReadOnlyList<AlertCondition> ForDeployments(AlertRule rule, IReadOnlyList<AlertDeploymentSnapshot> deployments) =>
        deployments
            .Where(d => rule.ServerId is null || d.ServerId == rule.ServerId)
            .Select(d =>
            {
                var key = d.ProjectId.ToString();
                if (d.Status != DeploymentStatus.Failed)
                    return new AlertCondition(key, d.ProjectName, d.ServerId, d.ServerName, AlertConditionState.Ok, null, $"{d.ProjectName} son deployment başarılı.");

                var reason = string.IsNullOrWhiteSpace(d.FailureReason) ? "neden kaydedilmedi" : d.FailureReason;
                return new AlertCondition(key, d.ProjectName, d.ServerId, d.ServerName, AlertConditionState.Firing, null,
                    $"{d.ProjectName} deployment'ı başarısız ({d.ServerName}): {reason}");
            }).ToList();

    public static IReadOnlyList<AlertCondition> ForBackups(AlertRule rule, IReadOnlyList<AlertBackupSnapshot> backups) =>
        backups
            .Where(b => rule.ServerId is null || b.ServerId == rule.ServerId)
            .Select(b =>
            {
                var key = b.JobId.ToString();
                if (b.Status != BackupRunStatus.Failed)
                    return new AlertCondition(key, b.JobName, b.ServerId, b.ServerName, AlertConditionState.Ok, null, $"{b.JobName}: son yedekleme başarılı.");

                var reason = string.IsNullOrWhiteSpace(b.FailureReason) ? "neden kaydedilmedi" : b.FailureReason;
                return new AlertCondition(key, b.JobName, b.ServerId, b.ServerName, AlertConditionState.Firing, null,
                    $"{b.JobName}: yedekleme başarısız ({b.ServerName}): {reason}");
            }).ToList();

    public static IReadOnlyList<AlertCondition> ForSecurity(AlertRule rule, IReadOnlyList<AlertSecuritySnapshot> scans) =>
        scans
            .Where(s => rule.ServerId is null || s.ServerId == rule.ServerId)
            .Select(s =>
            {
                var key = s.ServerId.ToString();
                return s.CriticalCount > 0
                    ? new AlertCondition(key, s.ServerName, s.ServerId, s.ServerName, AlertConditionState.Firing, s.CriticalCount,
                        $"{s.ServerName}: son güvenlik taramasında {s.CriticalCount} kritik bulgu (puan {s.Score}/100).")
                    : new AlertCondition(key, s.ServerName, s.ServerId, s.ServerName, AlertConditionState.Ok, 0,
                        $"{s.ServerName}: son güvenlik taramasında kritik bulgu yok (puan {s.Score}/100).");
            }).ToList();

    private static IEnumerable<AlertServerSnapshot> Scope(AlertRule rule, IReadOnlyList<AlertServerSnapshot> servers) =>
        rule.ServerId is { } serverId ? servers.Where(s => s.Id == serverId) : servers;

    private static AlertCondition Condition(AlertServerSnapshot server, AlertConditionState state, double? value, string message) =>
        new(server.Id.ToString(), server.Name, server.Id, server.Name, state, value, message);

    private static string DurationText(int durationMinutes) => durationMinutes > 0 ? $", {durationMinutes} dk boyunca" : string.Empty;
}

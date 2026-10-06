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
        TimeSpan sampleInterval,
        IReadOnlyDictionary<Guid, MetricWindowStats>? windowStats = null)
    {
        var name = AlertRuleKinds.DisplayName(rule.Kind);
        return Scope(rule, servers).Select(server =>
        {
            if (server.Status == ServerStatus.Maintenance)
                return Condition(server, AlertConditionState.Unknown, null, "Sunucu bakım modunda.");

            var serverSamples = samples.GetValueOrDefault(server.Id) ?? [];
            var (state, value) = windowStats is null
                ? MetricRuleEvaluator.Evaluate(serverSamples, rule.Kind, rule.Threshold, rule.DurationMinutes, now, freshness, sampleInterval)
                : MetricRuleEvaluator.EvaluateWindow(serverSamples, windowStats.GetValueOrDefault(server.Id), rule.Kind, rule.Threshold,
                    rule.DurationMinutes, now, freshness, sampleInterval);
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

    /// <summary>
    /// "Servis çalışmıyor": yönetilen servisin container'ı (kaynak geçmişi örneklerine göre) çalışmıyor veya sağlıksız ve bu durum
    /// kural süresi kadar sürüyor. Örnek yoksa veya eskiyse durum bilinmiyor sayılır (açık alarm kapanmaz).
    /// </summary>
    public static IReadOnlyList<AlertCondition> ForServiceDown(
        AlertRule rule,
        IReadOnlyList<AlertManagedServiceSnapshot> services,
        IReadOnlyList<AlertContainerSample> samples,
        DateTime now,
        TimeSpan freshness)
    {
        var byContainer = GroupSamples(samples);
        return ScopeServices(rule, services)
            .Where(s => s.Status != ManagedServiceStatus.Removing)
            .Select(service =>
            {
                var key = service.ServiceId.ToString();
                AlertCondition Make(AlertConditionState state, double? value, string message) =>
                    new(key, service.Name, service.ServerId, service.ServerName, state, value, message);

                if (service.ServerStatus == ServerStatus.Maintenance)
                    return Make(AlertConditionState.Unknown, null, "Sunucu bakım modunda.");
                if (service.ServerStatus == ServerStatus.Offline)
                    return Make(AlertConditionState.Unknown, null, "Sunucuya erişilemiyor; container durumu okunamadı.");
                if (service.Status is ManagedServiceStatus.Installing or ManagedServiceStatus.Updating)
                    return Make(AlertConditionState.Unknown, null, "Servis üzerinde işlem sürüyor.");

                var history = byContainer.GetValueOrDefault((service.ServerId, service.ContainerName)) ?? [];
                var latest = history.Count > 0 ? history[^1] : null;
                if (latest is null || now - latest.CollectedAt > freshness)
                    return Make(AlertConditionState.Unknown, null, "Container durumu henüz okunmadı (kaynak geçmişi toplanıyor mu?).");

                if (latest.IsUp)
                    return Make(AlertConditionState.Ok, 0, $"{service.Name} yeniden çalışıyor.");

                var downSince = latest.CollectedAt;
                for (var i = history.Count - 1; i >= 0 && !history[i].IsUp; i--)
                    downSince = history[i].CollectedAt;

                var minutes = Math.Max(0, Math.Floor((now - downSince).TotalMinutes));
                var state = minutes >= rule.DurationMinutes ? AlertConditionState.Firing : AlertConditionState.Unknown;
                var what = latest.IsRunning ? "sağlıksız (healthcheck başarısız)" : $"çalışmıyor (durum: {latest.State})";
                return Make(state, minutes,
                    $"{service.Name} servisinin container'ı ({service.ContainerName}) {what}; {service.ServerName} sunucusunda yaklaşık {minutes} dakikadır.");
            }).ToList();
    }

    /// <summary>
    /// "Container yeniden başlama döngüsü": kural süresi (pencere) içinde docker RestartCount artışı eşiğe ulaştı. Container yeniden
    /// oluşturulursa sayaç sıfırlanır; yalnızca artışlar toplanır.
    /// </summary>
    public static IReadOnlyList<AlertCondition> ForRestartLoop(
        AlertRule rule,
        IReadOnlyList<AlertServerSnapshot> servers,
        IReadOnlyList<AlertManagedServiceSnapshot> services,
        IReadOnlyList<AlertContainerSample> samples,
        DateTime now,
        TimeSpan freshness)
    {
        var window = TimeSpan.FromMinutes(Math.Max(1, rule.DurationMinutes));
        var windowStart = now - window;
        var serverById = servers.ToDictionary(s => s.Id);
        var serviceByContainer = services
            .GroupBy(s => (s.ServerId, s.ContainerName))
            .ToDictionary(g => g.Key, g => g.First());
        var target = rule.ManagedServiceId is { } serviceId ? services.FirstOrDefault(s => s.ServiceId == serviceId) : null;
        if (rule.ManagedServiceId is not null && target is null)
            return [];

        var conditions = new List<AlertCondition>();
        foreach (var ((serverId, container), history) in GroupSamples(samples))
        {
            if (rule.ServerId is { } scope && scope != serverId)
                continue;
            if (target is not null && (target.ServerId != serverId || target.ContainerName != container))
                continue;

            serverById.TryGetValue(serverId, out var server);
            serviceByContainer.TryGetValue((serverId, container), out var service);
            var key = AlertRuleKinds.ContainerTargetKey(serverId, container);
            var name = service is null ? container : $"{service.Name} ({container})";
            var serverName = server?.Name ?? service?.ServerName;
            AlertCondition Make(AlertConditionState state, double? value, string message) =>
                new(key, name, serverId, serverName, state, value, message);

            if (server?.Status == ServerStatus.Maintenance)
            {
                conditions.Add(Make(AlertConditionState.Unknown, null, "Sunucu bakım modunda."));
                continue;
            }

            var latest = history[^1];
            if (now - latest.CollectedAt > freshness)
            {
                conditions.Add(Make(AlertConditionState.Unknown, null, "Container için güncel örnek yok."));
                continue;
            }

            var baselineIndex = history.FindLastIndex(s => s.CollectedAt < windowStart);
            var start = Math.Max(0, baselineIndex);
            var increase = 0;
            for (var i = start + 1; i < history.Count; i++)
                increase += Math.Max(0, history[i].RestartCount - history[i - 1].RestartCount);

            var minutes = (int)window.TotalMinutes;
            conditions.Add(increase >= rule.Threshold
                ? Make(AlertConditionState.Firing, increase,
                    $"{name} son {minutes} dakikada {increase} kez yeniden başladı (eşik {AlertRuleKinds.Number(rule.Threshold)}); toplam {latest.RestartCount}, durum: {latest.State}.")
                : Make(AlertConditionState.Ok, increase, $"{name} son {minutes} dakikada {increase} kez yeniden başladı; döngü durdu."));
        }

        return conditions;
    }

    /// <summary>"Temizlenebilir alan": son temizlik taramasında silinebilir öğelerin toplamı eşiği (GB) aştı.</summary>
    public static IReadOnlyList<AlertCondition> ForReclaimable(AlertRule rule, IReadOnlyList<AlertReclaimableSnapshot> servers) =>
        servers
            .Where(s => rule.ServerId is null || s.ServerId == rule.ServerId)
            .Select(s =>
            {
                var key = s.ServerId.ToString();
                if (s.ServerStatus == ServerStatus.Maintenance)
                    return new AlertCondition(key, s.ServerName, s.ServerId, s.ServerName, AlertConditionState.Unknown, null, "Sunucu bakım modunda.");
                if (s.ScannedAt is null)
                    return new AlertCondition(key, s.ServerName, s.ServerId, s.ServerName, AlertConditionState.Unknown, null, "Sunucu henüz taranmadı.");

                var gigabytes = Math.Round(s.ReclaimableBytes / (double)AlertRuleKinds.BytesPerGigabyte, 1);
                return gigabytes >= rule.Threshold
                    ? new AlertCondition(key, s.ServerName, s.ServerId, s.ServerName, AlertConditionState.Firing, gigabytes,
                        $"{s.ServerName}: {AlertRuleKinds.Gigabytes(s.ReclaimableBytes)} temizlenebilir alan var (güvenli: {AlertRuleKinds.Gigabytes(s.SafeReclaimableBytes)}, eşik {AlertRuleKinds.Number(rule.Threshold)} GB). Sunucu → Temizlik sekmesinden temizleyebilirsiniz.")
                    : new AlertCondition(key, s.ServerName, s.ServerId, s.ServerName, AlertConditionState.Ok, gigabytes,
                        $"{s.ServerName}: temizlenebilir alan {AlertRuleKinds.Gigabytes(s.ReclaimableBytes)}; eşiğin altında.");
            }).ToList();

    private static Dictionary<(Guid ServerId, string Container), List<AlertContainerSample>> GroupSamples(IReadOnlyList<AlertContainerSample> samples) =>
        samples
            .GroupBy(s => (s.ServerId, s.ContainerName))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.CollectedAt).ToList());

    private static IEnumerable<AlertManagedServiceSnapshot> ScopeServices(AlertRule rule, IReadOnlyList<AlertManagedServiceSnapshot> services)
    {
        if (rule.ManagedServiceId is { } serviceId)
            return services.Where(s => s.ServiceId == serviceId);
        return rule.ServerId is { } serverId ? services.Where(s => s.ServerId == serverId) : services;
    }

    private static IEnumerable<AlertServerSnapshot> Scope(AlertRule rule, IReadOnlyList<AlertServerSnapshot> servers) =>
        rule.ServerId is { } serverId ? servers.Where(s => s.Id == serverId) : servers;

    private static AlertCondition Condition(AlertServerSnapshot server, AlertConditionState state, double? value, string message) =>
        new(server.Id.ToString(), server.Name, server.Id, server.Name, state, value, message);

    private static string DurationText(int durationMinutes) => durationMinutes > 0 ? $", {durationMinutes} dk boyunca" : string.Empty;
}

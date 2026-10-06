using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Alerting;

/// <summary>Bir değerlendirme turunda yalnızca etkin kuralların ihtiyaç duyduğu veriler ve her biri bir kez okunur.</summary>
public sealed class AlertEvaluationData
{
    /// <summary>Bu süreden uzun metrik kurallarında ham satırlar yüklenmez; pencere özeti veritabanında hesaplanır.</summary>
    public const int RawWindowMaxMinutes = 60;

    private readonly IAlertRepository _repository;
    private readonly IReadOnlyList<AlertRule> _rules;
    private readonly DateTime _now;
    private readonly TimeSpan _freshness;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<int, IReadOnlyDictionary<Guid, MetricWindowStats>> _windows = [];

    private IReadOnlyList<AlertServerSnapshot>? _servers;
    private IReadOnlyDictionary<Guid, IReadOnlyList<MetricSample>>? _samples;
    private IReadOnlyList<AlertUptimeSnapshot>? _uptime;
    private IReadOnlyList<AlertSslSnapshot>? _ssl;
    private IReadOnlyList<AlertDeploymentSnapshot>? _deployments;
    private IReadOnlyList<AlertBackupSnapshot>? _backups;
    private IReadOnlyList<AlertSecuritySnapshot>? _security;
    private IReadOnlyList<AlertContainerSample>? _containerSamples;
    private IReadOnlyList<AlertManagedServiceSnapshot>? _services;
    private IReadOnlyList<AlertReclaimableSnapshot>? _reclaimable;

    public AlertEvaluationData(IAlertRepository repository, IReadOnlyList<AlertRule> rules, DateTime now, TimeSpan freshness, CancellationToken cancellationToken)
    {
        _repository = repository;
        _rules = rules;
        _now = now;
        _freshness = freshness;
        _cancellationToken = cancellationToken;
    }

    public static bool UsesWindowStats(AlertRule rule) => AlertRuleKinds.IsMetric(rule.Kind) && rule.DurationMinutes > RawWindowMaxMinutes;

    public async Task<IReadOnlyList<AlertServerSnapshot>> ServersAsync() =>
        _servers ??= await _repository.GetServerSnapshotsAsync(_cancellationToken);

    /// <summary>
    /// Kısa süreli kuralların penceresi kadar ham örnek; uzun kurallar için yalnızca son örnek gereklidir (tazelik süresi).
    /// Tüm metrik kuralları belirli sunuculara bağlıysa yalnızca o sunucular okunur.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetricSample>>> SamplesAsync()
    {
        if (_samples is not null)
            return _samples;

        var maxRawDuration = MetricRules()
            .Where(r => !UsesWindowStats(r))
            .Select(r => r.DurationMinutes)
            .DefaultIfEmpty(0)
            .Max();
        var since = _now - TimeSpan.FromMinutes(maxRawDuration) - _freshness;
        var samples = await _repository.GetMetricSamplesAsync(since, ServerFilter(), _cancellationToken);
        _samples = samples.GroupBy(s => s.ServerId).ToDictionary(g => g.Key, g => (IReadOnlyList<MetricSample>)g.ToList());
        return _samples;
    }

    /// <summary>Verilen süredeki sunucu bazlı metrik özeti; aynı süreli kurallar tek sorguyu paylaşır.</summary>
    public async Task<IReadOnlyDictionary<Guid, MetricWindowStats>> WindowStatsAsync(int durationMinutes)
    {
        if (_windows.TryGetValue(durationMinutes, out var cached))
            return cached;

        var since = _now - TimeSpan.FromMinutes(durationMinutes);
        var stats = await _repository.GetMetricWindowStatsAsync(since, ServerFilter(), _cancellationToken);
        var result = stats.ToDictionary(s => s.ServerId);
        _windows[durationMinutes] = result;
        return result;
    }

    public async Task<IReadOnlyList<AlertUptimeSnapshot>> UptimeAsync() =>
        _uptime ??= await _repository.GetUptimeSnapshotsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertSslSnapshot>> SslAsync() =>
        _ssl ??= await _repository.GetSslSnapshotsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertDeploymentSnapshot>> DeploymentsAsync() =>
        _deployments ??= await _repository.GetLatestFinishedDeploymentsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertBackupSnapshot>> BackupsAsync() =>
        _backups ??= await _repository.GetLatestFinishedBackupsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertSecuritySnapshot>> SecurityAsync() =>
        _security ??= await _repository.GetLatestSecurityScansAsync(_cancellationToken);

    /// <summary>
    /// Servis kurallarının en uzun penceresi kadar (ve tazelik payı) container örneği; kurallar belirli sunuculara bağlıysa yalnızca onlar.
    /// </summary>
    public async Task<IReadOnlyList<AlertContainerSample>> ContainerSamplesAsync(TimeSpan containerFreshness)
    {
        if (_containerSamples is not null)
            return _containerSamples;

        var rules = _rules.Where(r => AlertRuleKinds.UsesContainerSamples(r.Kind)).ToList();
        var maxMinutes = rules.Select(r => r.DurationMinutes).DefaultIfEmpty(0).Max();
        var since = _now - TimeSpan.FromMinutes(maxMinutes) - containerFreshness * 2;
        IReadOnlyCollection<Guid>? filter = rules.Count == 0 || rules.Any(r => r.ServerId is null && r.ManagedServiceId is null)
            ? null
            : rules.Where(r => r.ServerId is not null).Select(r => r.ServerId!.Value)
                .Concat((await ServicesAsync()).Where(s => rules.Any(r => r.ManagedServiceId == s.ServiceId)).Select(s => s.ServerId))
                .Distinct()
                .ToList();
        _containerSamples = await _repository.GetContainerSamplesAsync(since, filter, _cancellationToken);
        return _containerSamples;
    }

    public async Task<IReadOnlyList<AlertManagedServiceSnapshot>> ServicesAsync() =>
        _services ??= await _repository.GetManagedServiceSnapshotsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertReclaimableSnapshot>> ReclaimableAsync() =>
        _reclaimable ??= await _repository.GetReclaimableSnapshotsAsync(_cancellationToken);

    private IEnumerable<AlertRule> MetricRules() => _rules.Where(r => AlertRuleKinds.IsMetric(r.Kind));

    /// <summary>Tüm sunucuları kapsayan bir metrik kuralı varsa filtre yoktur (null).</summary>
    private IReadOnlyCollection<Guid>? ServerFilter()
    {
        var rules = MetricRules().ToList();
        if (rules.Count == 0 || rules.Any(r => r.ServerId is null))
            return null;

        return rules.Select(r => r.ServerId!.Value).Distinct().ToList();
    }
}

using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Alerting;

/// <summary>Bir değerlendirme turunda yalnızca etkin kuralların ihtiyaç duyduğu veriler ve her biri bir kez okunur.</summary>
public sealed class AlertEvaluationData
{
    private readonly IAlertRepository _repository;
    private readonly IReadOnlyList<AlertRule> _rules;
    private readonly DateTime _now;
    private readonly TimeSpan _freshness;
    private readonly CancellationToken _cancellationToken;

    private IReadOnlyList<AlertServerSnapshot>? _servers;
    private IReadOnlyDictionary<Guid, IReadOnlyList<MetricSample>>? _samples;
    private IReadOnlyList<AlertUptimeSnapshot>? _uptime;
    private IReadOnlyList<AlertSslSnapshot>? _ssl;
    private IReadOnlyList<AlertDeploymentSnapshot>? _deployments;

    public AlertEvaluationData(IAlertRepository repository, IReadOnlyList<AlertRule> rules, DateTime now, TimeSpan freshness, CancellationToken cancellationToken)
    {
        _repository = repository;
        _rules = rules;
        _now = now;
        _freshness = freshness;
        _cancellationToken = cancellationToken;
    }

    public async Task<IReadOnlyList<AlertServerSnapshot>> ServersAsync() =>
        _servers ??= await _repository.GetServerSnapshotsAsync(_cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetricSample>>> SamplesAsync()
    {
        if (_samples is not null)
            return _samples;

        var maxDuration = _rules.Where(r => AlertRuleKinds.IsMetric(r.Kind)).Select(r => r.DurationMinutes).DefaultIfEmpty(0).Max();
        var since = _now - TimeSpan.FromMinutes(maxDuration) - _freshness;
        var samples = await _repository.GetMetricSamplesAsync(since, _cancellationToken);
        _samples = samples.GroupBy(s => s.ServerId).ToDictionary(g => g.Key, g => (IReadOnlyList<MetricSample>)g.ToList());
        return _samples;
    }

    public async Task<IReadOnlyList<AlertUptimeSnapshot>> UptimeAsync() =>
        _uptime ??= await _repository.GetUptimeSnapshotsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertSslSnapshot>> SslAsync() =>
        _ssl ??= await _repository.GetSslSnapshotsAsync(_cancellationToken);

    public async Task<IReadOnlyList<AlertDeploymentSnapshot>> DeploymentsAsync() =>
        _deployments ??= await _repository.GetLatestFinishedDeploymentsAsync(_cancellationToken);
}

using ServerManager.Application.Alerting;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Alerting;

public class AlertConditionEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private static AlertRule Rule(AlertRuleKind kind, double threshold = 0, int duration = 0, Guid? serverId = null) => new()
    {
        Name = "kural",
        Kind = kind,
        Threshold = threshold,
        DurationMinutes = duration,
        ServerId = serverId
    };

    private static AlertServerSnapshot Server(ServerStatus status, DateTime? lastSeen = null) =>
        new(Guid.NewGuid(), $"srv-{status}", status, lastSeen, Now.AddDays(-10));

    [Fact]
    public void Metrics_skip_servers_in_maintenance()
    {
        var server = Server(ServerStatus.Maintenance);
        var samples = new Dictionary<Guid, IReadOnlyList<MetricSample>> { [server.Id] = [new(server.Id, Now, 99, 0, 0)] };

        var condition = Assert.Single(AlertConditionEvaluator.ForMetrics(Rule(AlertRuleKind.CpuUsage, 80), [server], samples, Now, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30)));

        Assert.Equal(AlertConditionState.Unknown, condition.State);
    }

    [Fact]
    public void Metrics_rule_scoped_to_server_ignores_others()
    {
        var target = Server(ServerStatus.Healthy);
        var other = Server(ServerStatus.Healthy);

        var conditions = AlertConditionEvaluator.ForMetrics(Rule(AlertRuleKind.CpuUsage, 80, serverId: target.Id), [target, other],
            new Dictionary<Guid, IReadOnlyList<MetricSample>>(), Now, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30));

        Assert.Equal(target.Id.ToString(), Assert.Single(conditions).TargetKey);
    }

    [Fact]
    public void Offline_fires_only_after_duration()
    {
        var rule = Rule(AlertRuleKind.ServerOffline, duration: 5);
        var longOffline = Server(ServerStatus.Offline, Now.AddMinutes(-10));
        var justOffline = Server(ServerStatus.Offline, Now.AddMinutes(-2));
        var healthy = Server(ServerStatus.Healthy, Now);

        var conditions = AlertConditionEvaluator.ForServerOffline(rule, [longOffline, justOffline, healthy], Now);

        Assert.Equal(AlertConditionState.Firing, conditions[0].State);
        Assert.Equal(10, conditions[0].Value);
        Assert.Equal(AlertConditionState.Unknown, conditions[1].State);
        Assert.Equal(AlertConditionState.Ok, conditions[2].State);
    }

    [Fact]
    public void Uptime_down_fires_after_duration_and_up_recovers()
    {
        var rule = Rule(AlertRuleKind.UptimeCheckDown, duration: 2);
        AlertUptimeSnapshot Check(UptimeStatus status, int minutesAgo) =>
            new(Guid.NewGuid(), $"c-{status}-{minutesAgo}", null, null, status, Now.AddMinutes(-minutesAgo), 3, "Zaman aşımı");

        var conditions = AlertConditionEvaluator.ForUptime(rule,
            [Check(UptimeStatus.Down, 5), Check(UptimeStatus.Down, 1), Check(UptimeStatus.Up, 1), Check(UptimeStatus.Unknown, 0)], Now);

        Assert.Equal(AlertConditionState.Firing, conditions[0].State);
        Assert.Contains("Zaman aşımı", conditions[0].Message);
        Assert.Equal(AlertConditionState.Unknown, conditions[1].State);
        Assert.Equal(AlertConditionState.Ok, conditions[2].State);
        Assert.Equal(AlertConditionState.Unknown, conditions[3].State);
    }

    [Fact]
    public void Ssl_fires_below_threshold_days()
    {
        var rule = Rule(AlertRuleKind.SslCertificateExpiry, threshold: 30);
        AlertSslSnapshot Monitor(DateTime? notAfter) => new(Guid.NewGuid(), "example.com", 443, null, null, notAfter);

        var conditions = AlertConditionEvaluator.ForSsl(rule,
            [Monitor(Now.AddDays(10.5)), Monitor(Now.AddDays(60)), Monitor(Now.AddDays(-1)), Monitor(null)], Now);

        Assert.Equal(AlertConditionState.Firing, conditions[0].State);
        Assert.Equal(10, conditions[0].Value);
        Assert.Equal(AlertConditionState.Ok, conditions[1].State);
        Assert.Equal(AlertConditionState.Firing, conditions[2].State);
        Assert.Contains("doldu", conditions[2].Message);
        Assert.Equal(AlertConditionState.Unknown, conditions[3].State);
    }

    [Fact]
    public void Deployment_failure_fires_and_success_recovers()
    {
        var rule = Rule(AlertRuleKind.DeploymentFailed);
        var serverId = Guid.NewGuid();
        AlertDeploymentSnapshot Deployment(DeploymentStatus status) =>
            new(Guid.NewGuid(), $"proje-{status}", serverId, "web-01", Guid.NewGuid(), status,
                status == DeploymentStatus.Failed ? "build hatası" : null, Now.AddMinutes(-3));

        var conditions = AlertConditionEvaluator.ForDeployments(rule, [Deployment(DeploymentStatus.Failed), Deployment(DeploymentStatus.Succeeded)]);

        Assert.Equal(AlertConditionState.Firing, conditions[0].State);
        Assert.Contains("build hatası", conditions[0].Message);
        Assert.Equal(AlertConditionState.Ok, conditions[1].State);
    }
}

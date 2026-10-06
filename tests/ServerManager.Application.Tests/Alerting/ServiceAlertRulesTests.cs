using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.Validators.Alerting;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Alerting;

/// <summary>Servis çalışmıyor, yeniden başlama döngüsü ve temizlenebilir alan kuralları.</summary>
public class ServiceAlertRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 3, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(15);

    private static readonly Guid ServerId = Guid.NewGuid();

    private static AlertRule Rule(AlertRuleKind kind, double threshold = 0, int duration = 0, Guid? serverId = null, Guid? serviceId = null) => new()
    {
        Name = "kural",
        Kind = kind,
        Threshold = threshold,
        DurationMinutes = duration,
        ServerId = serverId,
        ManagedServiceId = serviceId
    };

    private static AlertManagedServiceSnapshot Service(
        string name = "db", ManagedServiceStatus status = ManagedServiceStatus.Running, ServerStatus serverStatus = ServerStatus.Healthy, Guid? serverId = null) =>
        new(Guid.NewGuid(), name, $"sm-svc-{name}", status, serverId ?? ServerId, "srv", serverStatus);

    private static AlertContainerSample Sample(string container, int minutesAgo, string state = "running", string? health = null, int restarts = 0, Guid? serverId = null) =>
        new(serverId ?? ServerId, container, Now.AddMinutes(-minutesAgo), state, health, restarts);

    // ---- Servis çalışmıyor

    [Fact]
    public void Service_down_fires_after_duration_of_consecutive_down_samples()
    {
        var service = Service();
        var samples = new[]
        {
            Sample(service.ContainerName, 20),
            Sample(service.ContainerName, 10, "exited"),
            Sample(service.ContainerName, 5, "exited"),
            Sample(service.ContainerName, 0, "exited")
        };

        var condition = Assert.Single(AlertConditionEvaluator.ForServiceDown(Rule(AlertRuleKind.ServiceDown, duration: 5), [service], samples, Now, Freshness));

        Assert.Equal(AlertConditionState.Firing, condition.State);
        Assert.Equal(service.ServiceId.ToString(), condition.TargetKey);
        Assert.Equal(10, condition.Value);
        Assert.Contains("çalışmıyor (durum: exited)", condition.Message);
    }

    [Fact]
    public void Service_down_waits_until_duration_passes()
    {
        var service = Service();
        var samples = new[] { Sample(service.ContainerName, 5), Sample(service.ContainerName, 0, "exited") };

        var condition = Assert.Single(AlertConditionEvaluator.ForServiceDown(Rule(AlertRuleKind.ServiceDown, duration: 10), [service], samples, Now, Freshness));

        Assert.Equal(AlertConditionState.Unknown, condition.State);
    }

    [Fact]
    public void Unhealthy_running_container_counts_as_down()
    {
        var service = Service();
        var condition = Assert.Single(AlertConditionEvaluator.ForServiceDown(
            Rule(AlertRuleKind.ServiceDown), [service], [Sample(service.ContainerName, 0, health: "unhealthy")], Now, Freshness));

        Assert.Equal(AlertConditionState.Firing, condition.State);
        Assert.Contains("sağlıksız", condition.Message);
    }

    [Theory]
    [InlineData(ManagedServiceStatus.Installing, ServerStatus.Healthy)]
    [InlineData(ManagedServiceStatus.Updating, ServerStatus.Healthy)]
    [InlineData(ManagedServiceStatus.Running, ServerStatus.Maintenance)]
    [InlineData(ManagedServiceStatus.Running, ServerStatus.Offline)]
    public void Service_down_is_unknown_during_operations_maintenance_or_offline(ManagedServiceStatus status, ServerStatus serverStatus)
    {
        var service = Service(status: status, serverStatus: serverStatus);
        var condition = Assert.Single(AlertConditionEvaluator.ForServiceDown(
            Rule(AlertRuleKind.ServiceDown), [service], [Sample(service.ContainerName, 0, "exited")], Now, Freshness));

        Assert.Equal(AlertConditionState.Unknown, condition.State);
    }

    [Fact]
    public void Stale_or_missing_samples_are_unknown_and_running_is_ok()
    {
        var stale = Service("a");
        var missing = Service("b");
        var healthy = Service("c");
        var samples = new[] { Sample(stale.ContainerName, 60, "exited"), Sample(healthy.ContainerName, 1, health: "healthy") };

        var conditions = AlertConditionEvaluator.ForServiceDown(Rule(AlertRuleKind.ServiceDown), [stale, missing, healthy], samples, Now, Freshness)
            .ToDictionary(c => c.TargetName);

        Assert.Equal(AlertConditionState.Unknown, conditions["a"].State);
        Assert.Equal(AlertConditionState.Unknown, conditions["b"].State);
        Assert.Equal(AlertConditionState.Ok, conditions["c"].State);
    }

    [Fact]
    public void Service_rule_can_target_one_service_or_one_server()
    {
        var target = Service("a");
        var other = Service("b");
        var elsewhere = Service("c", serverId: Guid.NewGuid());

        Assert.Equal("a", Assert.Single(AlertConditionEvaluator.ForServiceDown(
            Rule(AlertRuleKind.ServiceDown, serviceId: target.ServiceId), [target, other, elsewhere], [], Now, Freshness)).TargetName);
        Assert.Equal(2, AlertConditionEvaluator.ForServiceDown(
            Rule(AlertRuleKind.ServiceDown, serverId: ServerId), [target, other, elsewhere], [], Now, Freshness).Count);
    }

    // ---- Yeniden başlama döngüsü

    private static AlertServerSnapshot Server(ServerStatus status = ServerStatus.Healthy) => new(ServerId, "srv", status, Now, Now.AddDays(-1));

    [Fact]
    public void Restart_loop_counts_increases_inside_window_from_baseline()
    {
        var samples = new[]
        {
            Sample("web", 40, restarts: 2), // pencere öncesi taban
            Sample("web", 20, restarts: 3),
            Sample("web", 10, "restarting", restarts: 4),
            Sample("web", 0, "restarting", restarts: 5)
        };

        var condition = Assert.Single(AlertConditionEvaluator.ForRestartLoop(
            Rule(AlertRuleKind.ContainerRestartLoop, threshold: 3, duration: 30), [Server()], [], samples, Now, Freshness));

        Assert.Equal(AlertConditionState.Firing, condition.State);
        Assert.Equal(3, condition.Value);
        Assert.Equal(AlertRuleKinds.ContainerTargetKey(ServerId, "web"), condition.TargetKey);
    }

    [Fact]
    public void Restart_counter_reset_is_not_counted_as_negative_or_restart()
    {
        var samples = new[] { Sample("web", 20, restarts: 9), Sample("web", 10, restarts: 0), Sample("web", 0, restarts: 1) };

        var condition = Assert.Single(AlertConditionEvaluator.ForRestartLoop(
            Rule(AlertRuleKind.ContainerRestartLoop, threshold: 2, duration: 30), [Server()], [], samples, Now, Freshness));

        Assert.Equal(AlertConditionState.Ok, condition.State);
        Assert.Equal(1, condition.Value);
    }

    [Fact]
    public void Restart_loop_uses_service_name_and_can_be_limited_to_a_service()
    {
        var service = Service("redis");
        var samples = new[]
        {
            Sample(service.ContainerName, 10, restarts: 0), Sample(service.ContainerName, 0, restarts: 4),
            Sample("other", 10, restarts: 0), Sample("other", 0, restarts: 9)
        };

        var condition = Assert.Single(AlertConditionEvaluator.ForRestartLoop(
            Rule(AlertRuleKind.ContainerRestartLoop, threshold: 3, duration: 30, serviceId: service.ServiceId), [Server()], [service], samples, Now, Freshness));

        Assert.Equal("redis (sm-svc-redis)", condition.TargetName);
        Assert.Equal(AlertConditionState.Firing, condition.State);
    }

    [Fact]
    public void Restart_loop_is_unknown_for_stale_containers_or_maintenance()
    {
        var stale = AlertConditionEvaluator.ForRestartLoop(Rule(AlertRuleKind.ContainerRestartLoop, 1, 60), [Server()], [],
            [Sample("web", 50, restarts: 0), Sample("web", 40, restarts: 5)], Now, Freshness);
        Assert.Equal(AlertConditionState.Unknown, Assert.Single(stale).State);

        var maintenance = AlertConditionEvaluator.ForRestartLoop(Rule(AlertRuleKind.ContainerRestartLoop, 1, 60), [Server(ServerStatus.Maintenance)], [],
            [Sample("web", 5, restarts: 0), Sample("web", 0, restarts: 5)], Now, Freshness);
        Assert.Equal(AlertConditionState.Unknown, Assert.Single(maintenance).State);
    }

    // ---- Temizlenebilir alan

    [Fact]
    public void Reclaimable_space_fires_at_threshold_in_gigabytes()
    {
        var big = new AlertReclaimableSnapshot(Guid.NewGuid(), "büyük", ServerStatus.Healthy, Now, 12L * AlertRuleKinds.BytesPerGigabyte, 4L * AlertRuleKinds.BytesPerGigabyte);
        var small = new AlertReclaimableSnapshot(Guid.NewGuid(), "küçük", ServerStatus.Healthy, Now, AlertRuleKinds.BytesPerGigabyte, 0);
        var never = new AlertReclaimableSnapshot(Guid.NewGuid(), "hiç", ServerStatus.Healthy, null, 0, 0);

        var conditions = AlertConditionEvaluator.ForReclaimable(Rule(AlertRuleKind.ReclaimableSpace, 10), [big, small, never]).ToDictionary(c => c.TargetName);

        Assert.Equal(AlertConditionState.Firing, conditions["büyük"].State);
        Assert.Equal(12, conditions["büyük"].Value);
        Assert.Contains("12 GB", conditions["büyük"].Message);
        Assert.Equal(AlertConditionState.Ok, conditions["küçük"].State);
        Assert.Equal(AlertConditionState.Unknown, conditions["hiç"].State);
    }

    // ---- Reconciler entegrasyonu (histerezis)

    [Fact]
    public void Service_down_alert_opens_and_closes_after_two_ok_evaluations()
    {
        var service = Service();
        var rule = Rule(AlertRuleKind.ServiceDown);
        var down = AlertConditionEvaluator.ForServiceDown(rule, [service], [Sample(service.ContainerName, 0, "exited")], Now, Freshness);

        var opened = Assert.Single(AlertReconciler.Reconcile(rule, down, [], Now).Opened);
        Assert.Equal(AlertRuleKind.ServiceDown, opened.Kind);
        Assert.Equal(service.ServiceId.ToString(), opened.TargetKey);

        var up = AlertConditionEvaluator.ForServiceDown(rule, [service], [Sample(service.ContainerName, 0)], Now, Freshness);
        Assert.Empty(AlertReconciler.Reconcile(rule, up, [opened], Now).Recovered);
        Assert.Single(AlertReconciler.Reconcile(rule, up, [opened], Now).Recovered);
        Assert.Equal(AlertEventStatus.Resolved, opened.Status);
    }

    [Fact]
    public void Unknown_state_keeps_open_service_alert()
    {
        var service = Service();
        var rule = Rule(AlertRuleKind.ServiceDown);
        var opened = Assert.Single(AlertReconciler.Reconcile(rule,
            AlertConditionEvaluator.ForServiceDown(rule, [service], [Sample(service.ContainerName, 0, "exited")], Now, Freshness), [], Now).Opened);

        var unknown = AlertConditionEvaluator.ForServiceDown(rule, [service], [], Now, Freshness);
        var result = AlertReconciler.Reconcile(rule, unknown, [opened], Now);

        Assert.Empty(result.Recovered);
        Assert.Empty(result.Closed);
        Assert.Equal(AlertEventStatus.Firing, opened.Status);
    }

    [Fact]
    public void Message_builder_uses_new_kind_names()
    {
        var alert = new AlertEvent
        {
            RuleName = "Servisler", Kind = AlertRuleKind.ContainerRestartLoop, Severity = AlertSeverity.Critical,
            TargetName = "redis", Message = "redis 4 kez yeniden başladı", StartedAt = Now
        };

        var message = AlertMessageBuilder.Build(alert, NotificationKind.Firing, Now, null);

        Assert.Contains("Container yeniden başlama döngüsü", message.Body);
        Assert.StartsWith("[Kritik] redis", message.Title);
    }

    // ---- Kural türleri ve form

    [Theory]
    [InlineData(AlertRuleKind.ServiceDown, "none", true, true)]
    [InlineData(AlertRuleKind.ContainerRestartLoop, "count", true, true)]
    [InlineData(AlertRuleKind.ReclaimableSpace, "gb", false, false)]
    [InlineData(AlertRuleKind.DiskUsage, "percent", true, false)]
    public void Kind_metadata(AlertRuleKind kind, string unit, bool duration, bool service)
    {
        Assert.Equal(unit, AlertRuleKinds.ThresholdUnit(kind));
        Assert.Equal(duration, AlertRuleKinds.UsesDuration(kind));
        Assert.Equal(service, AlertRuleKinds.UsesService(kind));
        Assert.NotEqual(kind.ToString(), AlertRuleKinds.DisplayName(kind));
    }

    [Fact]
    public void Every_kind_has_turkish_name_and_description()
    {
        foreach (var kind in Enum.GetValues<AlertRuleKind>())
        {
            Assert.NotEqual(kind.ToString(), AlertRuleKinds.DisplayName(kind));
            Assert.False(string.IsNullOrWhiteSpace(AlertRuleKinds.Describe(kind, 3, 30)));
        }

        Assert.Equal("30 dk içinde ≥ 3 yeniden başlama", AlertRuleKinds.Describe(AlertRuleKind.ContainerRestartLoop, 3, 30));
        Assert.Equal((3.0, 30), AlertRuleKinds.Defaults(AlertRuleKind.ContainerRestartLoop));
        Assert.Equal((10.0, 0), AlertRuleKinds.Defaults(AlertRuleKind.ReclaimableSpace));
    }

    [Theory]
    [InlineData(AlertRuleKind.ContainerRestartLoop, 0, 30, "Threshold")]
    [InlineData(AlertRuleKind.ContainerRestartLoop, 3, 2, "DurationMinutes")]
    [InlineData(AlertRuleKind.ReclaimableSpace, 0, 0, "Threshold")]
    public void Validator_rejects_invalid_service_rule_values(AlertRuleKind kind, double threshold, int duration, string property)
    {
        var result = new AlertRuleFormDtoValidator().Validate(new AlertRuleFormDto { Name = "x", Kind = kind, Threshold = threshold, DurationMinutes = duration });

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Validator_accepts_defaults_for_new_kinds()
    {
        foreach (var kind in new[] { AlertRuleKind.ServiceDown, AlertRuleKind.ContainerRestartLoop, AlertRuleKind.ReclaimableSpace })
        {
            var (threshold, duration) = AlertRuleKinds.Defaults(kind);
            var result = new AlertRuleFormDtoValidator().Validate(new AlertRuleFormDto { Name = "x", Kind = kind, Threshold = threshold, DurationMinutes = duration });
            Assert.True(result.IsValid, string.Join(", ", result.Errors));
        }
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ServerManager.Application.Alerting;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Notifications;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class AlertServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IAlertRepository _repository = Substitute.For<IAlertRepository>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly NotificationChannel _channel = new() { Name = "ops", MinimumSeverity = AlertSeverity.Warning };
    private readonly Server _offline = new() { Id = Guid.NewGuid(), Name = "web-01" };

    public AlertServiceTests()
    {
        _repository.GetOpenEventsAsync(Arg.Any<CancellationToken>()).Returns([]);
        _repository.GetServerSnapshotsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new AlertServerSnapshot(_offline.Id, _offline.Name, ServerStatus.Offline, Now.UtcDateTime.AddMinutes(-10), Now.UtcDateTime.AddDays(-1))
        ]);
        _dispatcher.SendAsync(Arg.Any<NotificationChannel>(), Arg.Any<NotificationMessage>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success());
    }

    private AlertRule Rule(AlertRuleKind kind, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        Severity = AlertSeverity.Critical,
        Threshold = 80,
        Channels = [new AlertRuleChannel { ChannelId = _channel.Id, Channel = _channel }]
    };

    private AlertService Service() => new(
        _repository, _dispatcher, Substitute.For<IAuditLogService>(), Substitute.For<ICurrentUserService>(),
        new FixedTimeProvider(Now), Options.Create(new AlertingOptions()), Options.Create(new MonitoringOptions()),
        NullLogger<AlertService>.Instance);

    [Fact]
    public async Task Failing_rule_does_not_stop_other_rules()
    {
        var broken = Rule(AlertRuleKind.CpuUsage, "CPU");
        var offline = Rule(AlertRuleKind.ServerOffline, "Erişim");
        _repository.GetEnabledRulesAsync(Arg.Any<CancellationToken>()).Returns([broken, offline]);
        _repository.GetMetricSamplesAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<Guid>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("bozuk veri"));

        var result = await Service().EvaluateAsync(Ct);

        Assert.Equal(1, result.Opened);
        await _repository.Received(1).AddEventAsync(Arg.Is<AlertEvent>(e => e.RuleId == offline.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_alert_is_marked_notified_and_saved_after_successful_send()
    {
        var rule = Rule(AlertRuleKind.ServerOffline, "Erişim");
        _repository.GetEnabledRulesAsync(Arg.Any<CancellationToken>()).Returns([rule]);
        AlertEvent? added = null;
        await _repository.AddEventAsync(Arg.Do<AlertEvent>(e => added = e), Arg.Any<CancellationToken>());

        var result = await Service().EvaluateAsync(Ct);

        Assert.Equal(1, result.Deliveries);
        Assert.NotNull(added);
        Assert.Equal(Now.UtcDateTime, added!.LastNotifiedAt);
        await _repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_alert_stays_pending_when_every_channel_fails()
    {
        var rule = Rule(AlertRuleKind.ServerOffline, "Erişim");
        _repository.GetEnabledRulesAsync(Arg.Any<CancellationToken>()).Returns([rule]);
        _dispatcher.SendAsync(Arg.Any<NotificationChannel>(), Arg.Any<NotificationMessage>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Failure("kanal yanıt vermedi"));
        AlertEvent? added = null;
        await _repository.AddEventAsync(Arg.Do<AlertEvent>(e => added = e), Arg.Any<CancellationToken>());

        var result = await Service().EvaluateAsync(Ct);

        Assert.Equal(1, result.FailedDeliveries);
        Assert.Null(added!.LastNotifiedAt);
    }

    [Fact]
    public async Task Pending_open_alert_is_notified_on_next_evaluation()
    {
        var rule = Rule(AlertRuleKind.ServerOffline, "Erişim");
        var pending = new AlertEvent
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Kind = rule.Kind,
            Severity = rule.Severity,
            TargetKey = _offline.Id.ToString(),
            TargetName = _offline.Name,
            StartedAt = Now.UtcDateTime.AddMinutes(-5)
        };
        _repository.GetEnabledRulesAsync(Arg.Any<CancellationToken>()).Returns([rule]);
        _repository.GetOpenEventsAsync(Arg.Any<CancellationToken>()).Returns([pending]);

        var result = await Service().EvaluateAsync(Ct);

        Assert.Equal(0, result.Opened);
        Assert.Equal(1, result.Deliveries);
        Assert.Equal(Now.UtcDateTime, pending.LastNotifiedAt);
        await _dispatcher.Received(1).SendAsync(_channel, Arg.Is<NotificationMessage>(m => m.Kind == NotificationKind.Firing), pending.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AlertRuleKind.CpuUsage, 120, 2, false)]
    [InlineData(AlertRuleKind.CpuUsage, 121, 2, true)]
    [InlineData(AlertRuleKind.CpuUsage, 121, 1, true)]
    [InlineData(AlertRuleKind.DiskUsage, 1440, 24, false)]
    [InlineData(AlertRuleKind.ServerOffline, 1440, 2, false)]
    public void Metric_rule_duration_cannot_exceed_raw_retention(AlertRuleKind kind, int minutes, int retentionHours, bool exceeds)
    {
        var options = new MonitoringOptions { RawRetentionHours = retentionHours };

        Assert.Equal(exceeds, AlertRuleService.DurationExceedsRetention(kind, minutes, options));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 0, true)]
    [InlineData(0, 2, false)]
    public void Marks_notified_unless_every_channel_failed(int sent, int failed, bool expected)
    {
        Assert.Equal(expected, AlertService.ShouldMarkNotified(sent, failed));
    }
}

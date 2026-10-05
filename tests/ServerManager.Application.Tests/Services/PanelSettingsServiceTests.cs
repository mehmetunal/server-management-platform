using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Alerting;
using ServerManager.Application.Backups;
using ServerManager.Application.Cloud;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Security;
using ServerManager.Application.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Services;

public class PanelSettingsServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IPanelSettingRepository _repository = Substitute.For<IPanelSettingRepository>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IAlertRepository _alertRepository = Substitute.For<IAlertRepository>();
    private readonly MonitoringOptions _monitoring = new();
    private readonly AlertingOptions _alerting = new();
    private readonly BackupOptions _backup = new();
    private readonly SecurityScanOptions _scan = new();
    private readonly CloudOptions _cloud = new();
    private readonly RetentionOptions _retention = new();

    public PanelSettingsServiceTests()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        _alertRepository.GetRulesAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact]
    public async Task SaveAsync_rejects_interval_below_minimum()
    {
        var values = Current();
        values["Monitoring:IntervalSeconds"] = "5";

        var result = await Service().SaveAsync(values, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.PropertyName == "Monitoring:IntervalSeconds");
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<IReadOnlyList<PanelSetting>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_rejects_critical_threshold_at_or_below_warning()
    {
        var values = Current();
        values["Monitoring:CpuWarningPercent"] = "90";
        values["Monitoring:CpuCriticalPercent"] = "90";

        var result = await Service().SaveAsync(values, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.PropertyName == "Monitoring:CpuCriticalPercent");
    }

    [Fact]
    public async Task SaveAsync_rejects_unknown_time_zone()
    {
        var values = Current();
        values["Backup:TimeZone"] = "Not/AZone";

        var result = await Service().SaveAsync(values, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.PropertyName == "Backup:TimeZone");
    }

    [Fact]
    public async Task SaveAsync_persists_and_applies_a_changed_interval()
    {
        var values = Current();
        values["Monitoring:IntervalSeconds"] = "45";
        values["Monitoring:Enabled"] = "false";

        var result = await Service().SaveAsync(values, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(45, _monitoring.IntervalSeconds);
        Assert.False(_monitoring.Enabled);
        await _repository.Received(1).UpsertAsync(
            Arg.Is<IReadOnlyList<PanelSetting>>(rows => rows.Any(r => r.Key == "Monitoring:IntervalSeconds" && r.Value == "45")),
            Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(Arg.Any<ServerManager.Application.DTOs.AuditLogs.AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_rejects_raw_retention_shorter_than_a_metric_rule_window()
    {
        _alertRepository.GetRulesAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new AlertRule { Name = "Uzun CPU", Kind = AlertRuleKind.CpuUsage, DurationMinutes = 600 }
        ]);
        var values = Current();
        values["Monitoring:RawRetentionHours"] = "6";

        var result = await Service().SaveAsync(values, Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.PropertyName == "Monitoring:RawRetentionHours" && e.Message.Contains("10 saat"));
        Assert.Equal(48, _monitoring.RawRetentionHours);
    }

    [Fact]
    public async Task SaveAsync_applies_retention_days_and_allows_zero_for_keep_forever()
    {
        var values = Current();
        values["Retention:CommandRunDays"] = "0";
        values["Retention:DeploymentLogDays"] = "30";

        var result = await Service().SaveAsync(values, Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _retention.CommandRunDays);
        Assert.Equal(30, _retention.DeploymentLogDays);
    }

    [Fact]
    public void Groups_include_retention_section()
    {
        var group = Assert.Single(Service().GetGroups(), g => g.Title == "Kayıt saklama");

        Assert.Equal(6, group.Fields.Count);
        Assert.All(group.Fields, f => Assert.Equal("90", f.Value));
    }

    [Fact]
    public async Task SaveAsync_does_not_write_when_nothing_changed()
    {
        var result = await Service().SaveAsync(Current(), Ct);

        Assert.True(result.IsSuccess);
        await _repository.DidNotReceive().UpsertAsync(Arg.Any<IReadOnlyList<PanelSetting>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyStoredAsync_skips_a_corrupt_row_and_applies_the_rest()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new PanelSetting { Key = "Cloud:SyncIntervalHours", Value = "12" },
            new PanelSetting { Key = "Monitoring:IntervalSeconds", Value = "nope" }
        ]);

        await Service().ApplyStoredAsync(Ct);

        Assert.Equal(12, _cloud.SyncIntervalHours);
        Assert.Equal(30, _monitoring.IntervalSeconds);
    }

    private PanelSettingsService Service() =>
        new(_repository, _alertRepository, _audit, TimeProvider.System, NullLogger<PanelSettingsService>.Instance,
            Options.Create(_monitoring), Options.Create(_alerting), Options.Create(_backup), Options.Create(_scan), Options.Create(_cloud),
            Options.Create(_retention));

    private Dictionary<string, string> Current()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Service().GetGroups().SelectMany(g => g.Fields))
            values[field.Key] = field.Value;
        return values;
    }
}

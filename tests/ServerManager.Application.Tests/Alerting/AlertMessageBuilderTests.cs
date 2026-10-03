using ServerManager.Application.Alerting;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Alerting;

public class AlertMessageBuilderTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private static AlertEvent Alert(AlertSeverity severity = AlertSeverity.Critical) => new()
    {
        RuleName = "Yüksek CPU",
        Kind = AlertRuleKind.CpuUsage,
        Severity = severity,
        ServerName = "web-01",
        TargetName = "web-01",
        Message = "CPU %95",
        StartedAt = Now.AddMinutes(-90)
    };

    [Theory]
    [InlineData(NotificationKind.Firing, AlertSeverity.Critical, "[Kritik]")]
    [InlineData(NotificationKind.Firing, AlertSeverity.Warning, "[Uyarı]")]
    [InlineData(NotificationKind.Reminder, AlertSeverity.Critical, "[Sürüyor]")]
    [InlineData(NotificationKind.Recovery, AlertSeverity.Critical, "[Düzeldi]")]
    public void Title_reflects_kind_and_severity(NotificationKind kind, AlertSeverity severity, string prefix)
    {
        var message = AlertMessageBuilder.Build(Alert(severity), kind, Now, null);

        Assert.StartsWith(prefix, message.Title);
        Assert.Contains("web-01", message.Title);
    }

    [Fact]
    public void Recovery_body_uses_resolved_message_and_duration()
    {
        var alert = Alert();
        alert.ResolvedAt = Now;
        alert.ResolvedMessage = "CPU %20";

        var message = AlertMessageBuilder.Build(alert, NotificationKind.Recovery, Now, null);

        Assert.StartsWith("CPU %20", message.Body);
        Assert.Contains("Süre: 1 sa 30 dk", message.Body);
        Assert.DoesNotContain("Sunucu:", message.Body);
    }

    [Theory]
    [InlineData("https://panel.example.com/", "https://panel.example.com/Alerts")]
    [InlineData("https://panel.example.com/sm?x=1", "https://panel.example.com/sm/Alerts")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("", null)]
    public void Link_is_built_only_from_http_base_url(string baseUrl, string? expected)
    {
        Assert.Equal(expected, AlertMessageBuilder.Build(Alert(), NotificationKind.Firing, Now, baseUrl).Url);
    }

    [Theory]
    [InlineData(30, "1 dk'dan az")]
    [InlineData(300, "5 dk")]
    [InlineData(3_900, "1 sa 5 dk")]
    [InlineData(93_600, "1 gün 2 sa")]
    [InlineData(-10, "1 dk'dan az")]
    public void Duration_is_human_readable(int seconds, string expected)
    {
        Assert.Equal(expected, AlertMessageBuilder.Duration(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Test_message_is_marked()
    {
        var message = AlertMessageBuilder.BuildTest("Ekip", Now, null);

        Assert.Equal(NotificationKind.Test, message.Kind);
        Assert.Contains("Ekip", message.Title);
    }
}

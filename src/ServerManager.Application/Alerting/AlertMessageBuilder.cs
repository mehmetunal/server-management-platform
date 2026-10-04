using System.Text;
using ServerManager.Application;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public static class AlertMessageBuilder
{
    public const string AlertsPath = "/Alerts";

    public static NotificationMessage Build(AlertEvent alert, NotificationKind kind, DateTime now, string? publicBaseUrl)
    {
        var status = kind switch
        {
            NotificationKind.Recovery => "Düzeldi",
            NotificationKind.Reminder => "Sürüyor",
            _ => alert.Severity == AlertSeverity.Critical ? "Kritik" : "Uyarı"
        };
        var title = $"[{status}] {alert.TargetName}: {alert.RuleName}";

        var body = new StringBuilder();
        body.AppendLine(kind == NotificationKind.Recovery ? alert.ResolvedMessage ?? alert.Message : alert.Message);
        body.AppendLine();
        body.AppendLine($"Kural: {alert.RuleName} ({AlertRuleKinds.DisplayName(alert.Kind)})");
        if (!string.IsNullOrEmpty(alert.ServerName) && !string.Equals(alert.ServerName, alert.TargetName, StringComparison.Ordinal))
            body.AppendLine($"Sunucu: {alert.ServerName}");
        body.AppendLine($"Başlangıç: {AlertRuleKinds.Date(alert.StartedAt)} {alert.StartedAt:HH:mm} UTC");
        var end = alert.ResolvedAt ?? now;
        body.Append($"Süre: {Duration(end - alert.StartedAt)}");

        return new NotificationMessage(
            kind,
            alert.Severity,
            title,
            body.ToString(),
            now,
            alert.ServerName,
            BuildUrl(publicBaseUrl));
    }

    public static NotificationMessage BuildTest(string channelName, DateTime now, string? publicBaseUrl) =>
        new(NotificationKind.Test,
            AlertSeverity.Warning,
            $"[Test] {ProductInfo.Name}: {channelName}",
            "Bu bir test bildirimidir. Kanal doğru yapılandırılmış; alarmlar bu kanala gönderilecek.",
            now,
            null,
            BuildUrl(publicBaseUrl));

    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays} gün {duration.Hours} sa";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} sa {duration.Minutes} dk";
        return duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes} dk" : "1 dk'dan az";
    }

    private static string? BuildUrl(string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl)
            || !Uri.TryCreate(publicBaseUrl.Trim(), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + AlertsPath;
    }
}

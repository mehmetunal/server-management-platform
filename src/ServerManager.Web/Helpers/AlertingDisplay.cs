using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Alerting;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class AlertingDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string SeverityText(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "Kritik",
        AlertSeverity.Warning => "Uyarı",
        _ => severity.ToString()
    };

    public static string SeverityBadgeClass(AlertSeverity severity) =>
        severity == AlertSeverity.Critical ? "badge-danger" : "badge-warning";

    public static IEnumerable<SelectListItem> SeverityOptions(AlertSeverity? selected) =>
        Enum.GetValues<AlertSeverity>().Select(s => new SelectListItem(SeverityText(s), ((int)s).ToString(), s == selected));

    public static string EventStatusText(AlertEventStatus status) => status switch
    {
        AlertEventStatus.Firing => "Aktif",
        AlertEventStatus.Resolved => "Kapandı",
        _ => status.ToString()
    };

    public static IEnumerable<SelectListItem> EventStatusOptions(AlertEventStatus? selected) =>
        Enum.GetValues<AlertEventStatus>().Select(s => new SelectListItem(EventStatusText(s), ((int)s).ToString(), s == selected));

    public static IEnumerable<SelectListItem> KindOptions(AlertRuleKind selected) =>
        Enum.GetValues<AlertRuleKind>().Select(k => new SelectListItem(AlertRuleKinds.DisplayName(k), ((int)k).ToString(), k == selected));

    public static string KindIcon(AlertRuleKind kind) => kind switch
    {
        AlertRuleKind.CpuUsage or AlertRuleKind.MemoryUsage or AlertRuleKind.DiskUsage => "chart",
        AlertRuleKind.ServerOffline => "server",
        AlertRuleKind.UptimeCheckDown => "signal",
        AlertRuleKind.SslCertificateExpiry => "lock",
        AlertRuleKind.DeploymentFailed => "rocket",
        _ => "bell"
    };

    public static string NotificationKindText(NotificationKind kind) => kind switch
    {
        NotificationKind.Firing => "Alarm",
        NotificationKind.Recovery => "Düzeldi",
        NotificationKind.Reminder => "Hatırlatma",
        NotificationKind.Test => "Test",
        _ => kind.ToString()
    };

    public static string UptimeStatusText(UptimeStatus status) => status switch
    {
        UptimeStatus.Up => "Erişilebilir",
        UptimeStatus.Down => "Erişilemiyor",
        _ => "Bekleniyor"
    };

    public static string UptimeStatusBadgeClass(UptimeStatus status) => status switch
    {
        UptimeStatus.Up => "badge-success",
        UptimeStatus.Down => "badge-danger",
        _ => "badge-neutral"
    };

    public static IEnumerable<SelectListItem> UptimeStatusOptions(UptimeStatus? selected) =>
        Enum.GetValues<UptimeStatus>().Select(s => new SelectListItem(UptimeStatusText(s), ((int)s).ToString(), s == selected));

    public static string UptimeTypeText(UptimeCheckType type) => type switch
    {
        UptimeCheckType.Http => "HTTP(S)",
        UptimeCheckType.Tcp => "TCP port",
        _ => type.ToString()
    };

    public static string SslStatusText(SslCertificateStatus status) => status switch
    {
        SslCertificateStatus.Valid => "Geçerli",
        SslCertificateStatus.Expiring => "Süresi yaklaşıyor",
        SslCertificateStatus.Expired => "Süresi dolmuş",
        SslCertificateStatus.Invalid => "Geçersiz",
        SslCertificateStatus.Error => "Okunamadı",
        _ => "Bekleniyor"
    };

    public static string SslStatusBadgeClass(SslCertificateStatus status) => status switch
    {
        SslCertificateStatus.Valid => "badge-success",
        SslCertificateStatus.Expiring => "badge-warning",
        SslCertificateStatus.Expired or SslCertificateStatus.Invalid => "badge-danger",
        SslCertificateStatus.Error => "badge-dark",
        _ => "badge-neutral"
    };

    public static IEnumerable<SelectListItem> SslStatusOptions(SslCertificateStatus? selected) =>
        Enum.GetValues<SslCertificateStatus>().Select(s => new SelectListItem(SslStatusText(s), ((int)s).ToString(), s == selected));

    public static string DaysText(int? days) => days switch
    {
        null => "—",
        < 0 => $"{-days} gün önce doldu",
        0 => "Bugün doluyor",
        _ => $"{days} gün"
    };

    public static string Percent(double? value) =>
        value is null ? "—" : "%" + value.Value.ToString(value.Value >= 99.995 || value.Value == 0 ? "0" : "0.##", Turkish);

    public static string UptimeBarClass(double? value) => value switch
    {
        null => "uptime-bar-unknown",
        >= 99.5 => "uptime-bar-good",
        >= 95 => "uptime-bar-warn",
        _ => "uptime-bar-bad"
    };

    public static string Milliseconds(double? value) =>
        value is null ? "—" : $"{Math.Round(value.Value).ToString("N0", Turkish)} ms";

    public static string Interval(int seconds) => seconds switch
    {
        < 60 => $"{seconds} sn",
        _ when seconds % 3600 == 0 => $"{seconds / 3600} sa",
        _ when seconds % 60 == 0 => $"{seconds / 60} dk",
        _ => $"{seconds} sn"
    };

    public static string Duration(DateTime start, DateTime? end) =>
        AlertMessageBuilder.Duration((end ?? DateTime.UtcNow) - start);
}

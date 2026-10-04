using System.Globalization;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Web.Helpers;

public static class ServerSystemDisplay
{
    public const double DiskWarningPercent = 80;
    public const double DiskCriticalPercent = 90;

    public static readonly IReadOnlyList<(int Value, string Label)> Priorities =
    [
        (0, "Acil (emerg)"),
        (1, "Alarm (alert)"),
        (2, "Kritik (crit)"),
        (3, "Hata ve üstü (err)"),
        (4, "Uyarı ve üstü (warning)"),
        (5, "Bildirim ve üstü (notice)"),
        (6, "Bilgi ve üstü (info)"),
        (7, "Tümü (debug)")
    ];

    public static readonly IReadOnlyList<int> LineOptions = [100, 200, 500, 1000, 2000];

    public static string ManagerText(ServiceManagerKind manager) => manager switch
    {
        ServiceManagerKind.Systemd => "systemd",
        ServiceManagerKind.OpenRc => "OpenRC",
        _ => "Servis yöneticisi yok"
    };

    public static string ServiceStateText(ServiceUnit unit)
    {
        if (unit.IsFailed)
            return "Hatalı";
        if (unit.IsRunning)
            return unit.SubState == "exited" ? "Tamamlandı" : "Çalışıyor";
        return unit.ActiveState == "activating" ? "Başlıyor" : "Durdu";
    }

    public static string ServiceBadge(ServiceUnit unit)
    {
        if (unit.IsFailed)
            return "badge-danger";
        if (unit.IsRunning)
            return unit.SubState == "exited" ? "badge-neutral" : "badge-success";
        return unit.ActiveState == "activating" ? "badge-warning" : "badge-neutral";
    }

    public static string EnabledText(bool? enabled) => enabled switch
    {
        true => "Açık",
        false => "Kapalı",
        _ => "—"
    };

    public static string InterfaceBadge(string? state) => state?.ToUpperInvariant() switch
    {
        "UP" => "badge-success",
        "DOWN" => "badge-danger",
        _ => "badge-neutral"
    };

    public static string Percent(double? value) =>
        value is null ? "—" : "%" + value.Value.ToString("0.0", CultureInfo.GetCultureInfo("tr-TR"));

    public static string Kilobytes(long? kilobytes) =>
        kilobytes is null ? "—" : MetricDisplay.Bytes(kilobytes.Value * 1024);
}

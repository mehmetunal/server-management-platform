using ServerManager.Application.Security;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class SecurityDisplay
{
    private static readonly SecurityCheckStatus[] StatusOrder =
    [
        SecurityCheckStatus.Critical, SecurityCheckStatus.Warning, SecurityCheckStatus.Unknown, SecurityCheckStatus.Info, SecurityCheckStatus.Pass
    ];

    public static string StatusText(SecurityCheckStatus status) => status switch
    {
        SecurityCheckStatus.Pass => "Uygun",
        SecurityCheckStatus.Info => "Bilgi",
        SecurityCheckStatus.Warning => "Uyarı",
        SecurityCheckStatus.Critical => "Kritik",
        SecurityCheckStatus.Unknown => "Tespit edilemedi",
        _ => status.ToString()
    };

    public static string StatusBadgeClass(SecurityCheckStatus status) => status switch
    {
        SecurityCheckStatus.Pass => "badge-success",
        SecurityCheckStatus.Info => "badge-info",
        SecurityCheckStatus.Warning => "badge-warning",
        SecurityCheckStatus.Critical => "badge-danger",
        _ => "badge-neutral"
    };

    public static string StatusIcon(SecurityCheckStatus status) => status switch
    {
        SecurityCheckStatus.Pass => "check-circle",
        SecurityCheckStatus.Critical or SecurityCheckStatus.Warning => "warning",
        _ => "eye"
    };

    public static int StatusRank(SecurityCheckStatus status) => Array.IndexOf(StatusOrder, status);

    public static string ScoreClass(int? score) => score switch
    {
        null => "score-unknown",
        >= 85 => "score-good",
        >= 60 => "score-fair",
        _ => "score-poor"
    };

    public static string ScoreText(int? score) => score is null ? "—" : score.Value.ToString();

    public static string ScanStatusText(SecurityScanStatus status) => status switch
    {
        SecurityScanStatus.Running => "Sürüyor",
        SecurityScanStatus.Completed => "Tamamlandı",
        SecurityScanStatus.Failed => "Başarısız",
        _ => status.ToString()
    };

    public static string ScanStatusBadgeClass(SecurityScanStatus status) => status switch
    {
        SecurityScanStatus.Running => "badge-info",
        SecurityScanStatus.Completed => "badge-success",
        SecurityScanStatus.Failed => "badge-danger",
        _ => "badge-neutral"
    };

    public static string TriggerText(SecurityScanTrigger trigger) =>
        trigger == SecurityScanTrigger.Scheduled ? "Zamanlanmış" : "Elle";

    public static string ExposureText(PortExposure exposure) => exposure switch
    {
        PortExposure.Loopback => "Yalnızca yerel",
        PortExposure.AllInterfaces => "Tüm arayüzler",
        PortExposure.PrivateAddress => "Özel ağ",
        PortExposure.PublicAddress => "Genel adres",
        _ => exposure.ToString()
    };

    public static string ExposureBadgeClass(PortExposure exposure) => exposure switch
    {
        PortExposure.Loopback => "badge-success",
        PortExposure.PrivateAddress => "badge-info",
        _ => "badge-warning"
    };

    public static string BoolText(bool? value, string yes = "Evet", string no = "Hayır") => value switch
    {
        true => yes,
        false => no,
        _ => "Bilinmiyor"
    };
}

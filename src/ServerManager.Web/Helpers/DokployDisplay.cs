using ServerManager.Application.Dokploy;
using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class DokployDisplay
{
    /// <summary>Sihirbazdaki "Uyumluluk", "Docker" ve "Port" adımlarının hangi kontrolleri gösterdiği.</summary>
    public static readonly IReadOnlyList<(string Step, string Title, string[] Keys)> CompatibilityGroups =
    [
        ("compatibility", "Sistem uyumluluğu", ["os", "arch", "container", "privileges", "memory", "disk", "internet"]),
        ("docker", "Docker", ["docker", "swarm"]),
        ("ports", "Portlar", ["ports"])
    ];

    public static string StatusText(DokployStatus status) => status switch
    {
        DokployStatus.Running => "Çalışıyor",
        DokployStatus.Degraded => "Sorunlu",
        DokployStatus.Stopped => "Durdu",
        DokployStatus.Unreachable => "Ulaşılamıyor",
        DokployStatus.NotInstalled => "Kurulu değil",
        _ => "Bilinmiyor"
    };

    public static string StatusBadgeClass(DokployStatus status) => status switch
    {
        DokployStatus.Running => "badge-success",
        DokployStatus.Degraded => "badge-warning",
        DokployStatus.Stopped or DokployStatus.Unreachable => "badge-danger",
        _ => "badge-neutral"
    };

    public static string InstallationStatusText(DokployInstallationStatus status) => status switch
    {
        DokployInstallationStatus.Running => "Sürüyor",
        DokployInstallationStatus.Succeeded => "Başarılı",
        DokployInstallationStatus.Failed => "Başarısız",
        DokployInstallationStatus.Interrupted => "Kesildi",
        _ => status.ToString()
    };

    public static string InstallationStatusBadgeClass(DokployInstallationStatus status) => status switch
    {
        DokployInstallationStatus.Running => "badge-info",
        DokployInstallationStatus.Succeeded => "badge-success",
        DokployInstallationStatus.Failed => "badge-danger",
        _ => "badge-warning"
    };

    public static string CheckText(DokployCheckStatus status) => status switch
    {
        DokployCheckStatus.Passed => "Uygun",
        DokployCheckStatus.Warning => "Uyarı",
        _ => "Engel"
    };

    public static string CheckBadgeClass(DokployCheckStatus status) => status switch
    {
        DokployCheckStatus.Passed => "badge-success",
        DokployCheckStatus.Warning => "badge-warning",
        _ => "badge-danger"
    };

    public static string CheckIcon(DokployCheckStatus status) => status switch
    {
        DokployCheckStatus.Passed => "check-circle",
        _ => "warning"
    };

    /// <summary>Bir grubun genel sonucu: en kötü kontrolün durumu.</summary>
    public static DokployCheckStatus GroupStatus(IEnumerable<DokployCompatibilityCheckDto> checks) =>
        checks.Select(c => c.Status).DefaultIfEmpty(DokployCheckStatus.Passed).Max();

    public static string Replicas(DokployServiceDto service) => $"{service.RunningReplicas}/{service.DesiredReplicas}";

    public static string ResponseTime(int? milliseconds) => milliseconds is null ? "—" : $"{milliseconds} ms";
}

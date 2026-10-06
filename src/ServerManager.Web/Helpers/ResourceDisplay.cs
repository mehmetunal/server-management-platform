using System.Globalization;
using ServerManager.Application.ResourceUsage;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Web.Helpers;

public static class ResourceDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string SeverityBadge(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Critical => "badge-danger",
        FindingSeverity.Warning => "badge-warning",
        FindingSeverity.Info => "badge-info",
        _ => "badge-success"
    };

    public static string SeverityText(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Critical => "Kritik",
        FindingSeverity.Warning => "Uyarı",
        FindingSeverity.Info => "Bilgi",
        _ => "Normal"
    };

    /// <summary>Sayfa içi bölüm bağlantısı; Temizlik ayrı sayfadır ve controller'da üretilir.</summary>
    public static (string Anchor, string Label)? SectionLink(FindingAction action) => action switch
    {
        FindingAction.Cpu => ("#resources-cpu", "CPU ayrıntısı"),
        FindingAction.Memory => ("#resources-memory", "Bellek ayrıntısı"),
        FindingAction.Disk => ("#resources-disk", "Diskler"),
        FindingAction.Io => ("#resources-io", "G/Ç yapan process'ler"),
        FindingAction.Processes => ("#resources-processes", "Process'ler"),
        FindingAction.Containers => ("#resources-containers", "Container'lar"),
        FindingAction.Cleanup => ("#resources-disk", "Diskler"),
        _ => null
    };

    public static string Number(double? value, string format = "0.##") =>
        value is null ? "—" : value.Value.ToString(format, Turkish);

    public static string Percent(double? value) =>
        value is null ? "—" : "%" + value.Value.ToString("0.#", Turkish);

    public static double Share(long part, long total) => total > 0 ? Math.Clamp(part * 100d / total, 0, 100) : 0;

    public static string OwnerText(PanelOwner owner) => owner.Kind switch
    {
        PanelOwnerKind.Project => owner.Name ?? owner.Slug ?? "Proje",
        PanelOwnerKind.Service => owner.Name ?? owner.Slug ?? "Servis",
        PanelOwnerKind.Proxy => "Traefik (panel)",
        _ => "Panel"
    };

    public static string OwnerKindText(PanelOwner owner) => owner.Kind switch
    {
        PanelOwnerKind.Project => "Proje",
        PanelOwnerKind.Service => "Servis",
        _ => "Panel"
    };

    public static string LoadBarClass(double? load, int? cores) =>
        load is null || cores is not > 0
            ? "bg-slate-300 dark:bg-slate-700"
            : load / cores > ResourceHeuristics.LoadCriticalPerCore ? "bg-red-500"
            : load / cores > ResourceHeuristics.LoadWarningPerCore ? "bg-amber-500"
            : "bg-emerald-500";

    public static string BarWidth(double percent) =>
        Math.Clamp(percent, 0, 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}

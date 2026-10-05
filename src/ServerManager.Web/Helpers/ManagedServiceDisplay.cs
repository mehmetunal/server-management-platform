using System.Globalization;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class ManagedServiceDisplay
{
    public static string StatusText(ManagedServiceStatus status) => status switch
    {
        ManagedServiceStatus.Installing => "Kuruluyor",
        ManagedServiceStatus.Running => "Çalışıyor",
        ManagedServiceStatus.Failed => "Hatalı",
        ManagedServiceStatus.Updating => "Güncelleniyor",
        ManagedServiceStatus.Removing => "Kaldırılıyor",
        ManagedServiceStatus.Removed => "Kaldırıldı",
        _ => status.ToString()
    };

    public static string StatusBadgeClass(ManagedServiceStatus status) => status switch
    {
        ManagedServiceStatus.Running => "badge-success",
        ManagedServiceStatus.Failed => "badge-danger",
        ManagedServiceStatus.Installing or ManagedServiceStatus.Updating or ManagedServiceStatus.Removing => "badge-info",
        _ => "badge-neutral"
    };

    public static string CategoryText(ManagedServiceCategory category) => category switch
    {
        ManagedServiceCategory.Database => "Veritabanları",
        ManagedServiceCategory.Application => "Uygulamalar",
        _ => category.ToString()
    };

    public static string KindText(ManagedServiceOperationKind kind) => kind switch
    {
        ManagedServiceOperationKind.Install => "Kurulum",
        ManagedServiceOperationKind.Recreate => "Yeniden oluşturma",
        ManagedServiceOperationKind.Upgrade => "Sürüm yükseltme",
        ManagedServiceOperationKind.Remove => "Kaldırma",
        _ => kind.ToString()
    };

    public static string OperationStatusText(ManagedServiceOperationStatus status) => status switch
    {
        ManagedServiceOperationStatus.Running => "Sürüyor",
        ManagedServiceOperationStatus.Succeeded => "Başarılı",
        ManagedServiceOperationStatus.Failed => "Başarısız",
        ManagedServiceOperationStatus.Interrupted => "Kesildi",
        _ => status.ToString()
    };

    public static string OperationStatusBadgeClass(ManagedServiceOperationStatus status) => status switch
    {
        ManagedServiceOperationStatus.Running => "badge-info",
        ManagedServiceOperationStatus.Succeeded => "badge-success",
        ManagedServiceOperationStatus.Failed => "badge-danger",
        _ => "badge-warning"
    };

    public static IReadOnlyList<(ServiceOperationStage Stage, string Label)> Steps(ManagedServiceOperationKind kind) =>
        kind == ManagedServiceOperationKind.Remove ? ServiceOperationStages.Remove : ServiceOperationStages.Install;

    public static string PortText(PublishedPort port) =>
        string.Create(CultureInfo.InvariantCulture, $"{port.BindAddress}:{port.HostPort} → {port.ContainerPort}");

    public static string LogoStyle(ServiceTemplate? template) =>
        $"--service-color: {template?.Color ?? "#64748b"}";

    public static string Duration(DateTime started, DateTime? finished)
    {
        if (finished is null)
            return "—";

        var span = finished.Value - started;
        return span.TotalMinutes >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalMinutes} dk {span.Seconds} sn")
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)span.TotalSeconds)} sn");
    }
}

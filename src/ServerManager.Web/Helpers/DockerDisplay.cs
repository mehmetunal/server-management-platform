using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Web.Helpers;

public static class DockerDisplay
{
    public static string StateText(string state) => state.ToLowerInvariant() switch
    {
        "running" => "Çalışıyor",
        "exited" => "Durdu",
        "paused" => "Duraklatıldı",
        "restarting" => "Yeniden başlıyor",
        "created" => "Oluşturuldu",
        "removing" => "Siliniyor",
        "dead" => "Ölü",
        _ => string.IsNullOrEmpty(state) ? "Bilinmiyor" : state
    };

    public static string StateBadgeClass(DockerContainerDto container) => container.State.ToLowerInvariant() switch
    {
        "running" when container.Health == "unhealthy" => "badge-warning",
        "running" => "badge-success",
        "paused" => "badge-info",
        "restarting" => "badge-warning",
        "exited" when container.ExitCode is > 0 => "badge-danger",
        "dead" => "badge-danger",
        _ => "badge-neutral"
    };

    public static string? HealthText(string? health) => health switch
    {
        "healthy" => "Sağlıklı",
        "unhealthy" => "Sağlıksız",
        "starting" => "Kontrol ediliyor",
        _ => null
    };

    public static string HealthBadgeClass(string? health) => health switch
    {
        "healthy" => "badge-success",
        "unhealthy" => "badge-danger",
        _ => "badge-neutral"
    };

    public static string DiskUsageTypeText(string type) => type switch
    {
        "Images" => "Image'lar",
        "Containers" => "Container'lar",
        "Local Volumes" => "Volume'lar",
        "Build Cache" => "Build cache",
        _ => type
    };

    public static bool IsRunning(DockerContainerDto container) =>
        string.Equals(container.State, "running", StringComparison.OrdinalIgnoreCase);

    public static bool IsPaused(DockerContainerDto container) =>
        string.Equals(container.State, "paused", StringComparison.OrdinalIgnoreCase);
}

using Microsoft.AspNetCore.Mvc.Rendering;
using ServerManager.Application.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class DeploymentDisplay
{
    public static string StatusText(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Started => "Başladı",
        DeploymentStatus.Building => "Build ediliyor",
        DeploymentStatus.Deploying => "Deploy ediliyor",
        DeploymentStatus.Succeeded => "Başarılı",
        DeploymentStatus.Failed => "Başarısız",
        DeploymentStatus.Cancelled => "İptal edildi",
        DeploymentStatus.Interrupted => "Kesildi",
        _ => status.ToString()
    };

    public static string StatusBadgeClass(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Started or DeploymentStatus.Building or DeploymentStatus.Deploying => "badge-info",
        DeploymentStatus.Succeeded => "badge-success",
        DeploymentStatus.Failed => "badge-danger",
        DeploymentStatus.Cancelled or DeploymentStatus.Interrupted => "badge-warning",
        _ => "badge-neutral"
    };

    public static string KindText(DeploymentKind kind) => kind switch
    {
        DeploymentKind.Rollback => "Geri dönüş",
        DeploymentKind.Restart => "Yeniden başlatma",
        _ => "Deploy"
    };

    public static string KindHint(DeploymentKind kind) => kind switch
    {
        DeploymentKind.Rollback => "Önceki bir sürüme geri dönüldü; imaj sunucuda duruyorsa build yapılmadı.",
        DeploymentKind.Restart => "Build olmadan .env yeniden yazıldı ve container'lar yeniden oluşturuldu.",
        _ => "Kaynak kod çekildi, build edildi ve çalıştırıldı."
    };

    public static IEnumerable<SelectListItem> StatusOptions(DeploymentStatus? selected) =>
        Enum.GetValues<DeploymentStatus>().Select(s => new SelectListItem(StatusText(s), ((int)s).ToString(), s == selected));

    public static string BuildTypeText(DeploymentBuildType type) => type switch
    {
        DeploymentBuildType.DockerCompose => "Docker Compose",
        DeploymentBuildType.Dockerfile => "Dockerfile",
        DeploymentBuildType.Commands => "Komutlar",
        _ => type.ToString()
    };

    public static string BuildTypeHint(DeploymentBuildType type) => type switch
    {
        DeploymentBuildType.DockerCompose => "docker compose build ve up -d ile çalışır.",
        DeploymentBuildType.Dockerfile => "Image build edilir ve tek container olarak çalıştırılır.",
        DeploymentBuildType.Commands => "Proje klasöründe tanımladığınız komutlar çalışır.",
        _ => string.Empty
    };

    public static IEnumerable<SelectListItem> BuildTypeOptions(DeploymentBuildType selected) =>
        Enum.GetValues<DeploymentBuildType>().Select(t => new SelectListItem(BuildTypeText(t), ((int)t).ToString(), t == selected));

    public static string GitProviderText(GitProvider provider) => provider switch
    {
        GitProvider.GitHub => "GitHub",
        GitProvider.GitLab => "GitLab",
        GitProvider.Bitbucket => "Bitbucket",
        GitProvider.SelfHosted => "Diğer / kendi sunucusu",
        _ => provider.ToString()
    };

    public static IEnumerable<SelectListItem> GitProviderOptions(GitProvider selected) =>
        Enum.GetValues<GitProvider>().Select(p => new SelectListItem(GitProviderText(p), ((int)p).ToString(), p == selected));

    public static string TlsModeText(DeploymentTlsMode mode) => mode switch
    {
        DeploymentTlsMode.Cloudflare => "Cloudflare",
        DeploymentTlsMode.LetsEncrypt => "Let's Encrypt",
        DeploymentTlsMode.Custom => "Özel sertifika",
        _ => mode.ToString()
    };

    public static IEnumerable<SelectListItem> TlsModeOptions(DeploymentTlsMode selected) =>
        Enum.GetValues<DeploymentTlsMode>().Select(mode => new SelectListItem(TlsModeText(mode), ((int)mode).ToString(), mode == selected));

    public static string ShortSha(string? sha) => string.IsNullOrEmpty(sha) ? "—" : GitRefs.ShortSha(sha);

    public static string Duration(DateTime startedAt, DateTime? completedAt)
    {
        if (completedAt is null)
            return "—";

        var duration = completedAt.Value - startedAt;
        if (duration < TimeSpan.Zero)
            return "—";

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} sa {duration.Minutes} dk"
            : duration.TotalMinutes >= 1
                ? $"{(int)duration.TotalMinutes} dk {duration.Seconds} sn"
                : $"{Math.Max(1, (int)duration.TotalSeconds)} sn";
    }
}

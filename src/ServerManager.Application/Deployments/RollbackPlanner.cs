using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Deployments;

public enum RollbackStrategy
{
    /// <summary>Dockerfile projesinde o commit'in imajı (<c>sm-&lt;slug&gt;:&lt;kısa-sha&gt;</c>) sunucuda duruyor; build yapılmaz.</summary>
    RunExistingImage,

    /// <summary>Commit yeniden çekilip build edilir (Compose, komut projeleri veya imajı silinmiş Dockerfile projesi).</summary>
    RebuildCommit
}

/// <summary>Geri dönüş kuralları: hangi deployment'a dönülebileceği ve dönüşün nasıl yapılacağı.</summary>
public static class RollbackPlanner
{
    /// <summary>Geri dönülebilecek deployment için hata mesajı; dönülebiliyorsa null.</summary>
    public static string? Validate(Deployment source, DeploymentProject? project)
    {
        if (project is null)
            return "Proje silindiği için geri dönülemez.";

        if (source.ProjectId != project.Id)
            return "Deployment bu projeye ait değil.";

        if (source.Status != DeploymentStatus.Succeeded)
            return "Yalnızca başarıyla tamamlanmış bir deployment'a geri dönülebilir.";

        if (!GitRefs.IsValidCommit(source.CommitSha))
            return "Bu deployment'ın commit bilgisi yok; geri dönülemez.";

        return null;
    }

    public static RollbackStrategy Choose(DeploymentBuildType buildType, bool imageExists) =>
        buildType == DeploymentBuildType.Dockerfile && imageExists ? RollbackStrategy.RunExistingImage : RollbackStrategy.RebuildCommit;

    /// <summary>
    /// Bir proje için en son başarılı deployment "şu an çalışan" sürümdür; liste ekranında ona geri dönüş düğmesi gösterilmez.
    /// </summary>
    public static bool CanOfferRollback(Deployment deployment, Guid? currentDeploymentId) =>
        deployment.Status == DeploymentStatus.Succeeded
        && GitRefs.IsValidCommit(deployment.CommitSha)
        && deployment.Id != currentDeploymentId;
}

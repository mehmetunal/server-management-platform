namespace ServerManager.Domain.Enums;

public enum DeploymentKind
{
    /// <summary>Kaynak kod çekilir, build edilir ve çalıştırılır (elle, yeniden deploy veya webhook).</summary>
    Deploy = 0,

    /// <summary>Önceki başarılı bir sürüme dönüş; Dockerfile'da imaj hâlâ varsa yeniden build edilmez.</summary>
    Rollback = 1,

    /// <summary>Kaynak kod ve build olmadan .env ve compose override yeniden yazılır, container'lar yeniden oluşturulur.</summary>
    Restart = 2
}

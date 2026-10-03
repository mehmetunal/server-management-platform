namespace ServerManager.Web.Deployments;

/// <param name="Sequence">Çıktının hangi parçaya kadar <paramref name="Output"/> içinde olduğunu gösterir; istemci bu numaraya kadar gelen canlı parçaları yok sayar.</param>
/// <param name="Stage">Ulaşılan son ilerleme aşaması (Preparing, Source, Building, Deploying, Completed).</param>
/// <param name="Status">Bittiyse kayıttaki sonuç (<c>DeploymentStatus</c> adı).</param>
public sealed record DeploymentJoinResponse(
    bool IsSuccess,
    string? Message,
    string Output = "",
    long Sequence = 0,
    string? Stage = null,
    bool IsCompleted = false,
    string? Status = null);

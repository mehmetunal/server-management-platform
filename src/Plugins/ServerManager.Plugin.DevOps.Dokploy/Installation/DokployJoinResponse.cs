namespace ServerManager.Plugin.DevOps.Dokploy.Installation;

/// <param name="Sequence">Çıktının hangi parçaya kadar <paramref name="Output"/> içinde olduğunu gösterir; istemci bu numaraya kadar gelen canlı parçaları yok sayar.</param>
public sealed record DokployJoinResponse(
    bool IsSuccess,
    string? Message,
    string Output = "",
    long Sequence = 0,
    string? Stage = null,
    string? StageMessage = null,
    bool IsCompleted = false,
    bool Succeeded = false);

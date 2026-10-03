namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed class DokployScriptResult
{
    public string? Sha256 { get; init; }

    public int? ExitCode { get; init; }

    public bool TimedOut { get; init; }

    /// <summary>Betik çalıştırılamadıysa veya hata ile bittiyse kullanıcıya gösterilecek açıklama.</summary>
    public string? ErrorMessage { get; init; }

    public bool IsSuccess => !TimedOut && ExitCode == 0 && ErrorMessage is null;
}

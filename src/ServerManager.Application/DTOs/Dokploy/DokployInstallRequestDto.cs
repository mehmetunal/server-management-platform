namespace ServerManager.Application.DTOs.Dokploy;

public sealed class DokployInstallRequestDto
{
    /// <summary>Boşsa son kararlı sürüm; "canary", "latest" veya "v0.25.3" biçiminde olabilir.</summary>
    public string? Version { get; set; }

    public string? ConfirmationName { get; set; }
}

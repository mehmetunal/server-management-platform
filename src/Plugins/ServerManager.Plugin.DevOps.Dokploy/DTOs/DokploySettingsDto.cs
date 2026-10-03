namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed class DokploySettingsDto
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Boş bırakılırsa kayıtlı anahtar korunur.</summary>
    public string? ApiKey { get; set; }
}

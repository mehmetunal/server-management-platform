namespace ServerManager.Application.DTOs.Dokploy;

public sealed class DokploySettingsDto
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Boş bırakılırsa kayıtlı anahtar korunur.</summary>
    public string? ApiKey { get; set; }
}

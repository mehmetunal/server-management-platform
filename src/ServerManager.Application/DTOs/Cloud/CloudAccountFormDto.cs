namespace ServerManager.Application.DTOs.Cloud;

public sealed class CloudAccountFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    /// <summary>Düzenlemede boş bırakılırsa kayıtlı anahtar korunur.</summary>
    public string? Token { get; set; }
}

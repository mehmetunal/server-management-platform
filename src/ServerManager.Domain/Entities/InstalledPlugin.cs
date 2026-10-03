namespace ServerManager.Domain.Entities;

/// <summary>Kurulmuş bir eklentinin durumu. Eklenti dosyaları silinse bile kayıt ve eklentinin verisi korunur.</summary>
public class InstalledPlugin
{
    public string SystemName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTime InstalledAt { get; set; }

    public string? InstalledBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}

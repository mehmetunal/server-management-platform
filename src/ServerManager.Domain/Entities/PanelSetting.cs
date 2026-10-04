namespace ServerManager.Domain.Entities;

/// <summary>Panelden değiştirilen çalışma ayarı. appsettings değerinin üzerine yazılır; sır yoksa satır da yoktur.</summary>
public class PanelSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}

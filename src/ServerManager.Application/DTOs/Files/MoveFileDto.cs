namespace ServerManager.Application.DTOs.Files;

/// <summary>Yeniden adlandırma ve taşıma; hedef tam yoldur.</summary>
public sealed class MoveFileDto
{
    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;
}

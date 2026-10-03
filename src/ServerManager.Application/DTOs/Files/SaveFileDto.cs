namespace ServerManager.Application.DTOs.Files;

public sealed class SaveFileDto
{
    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool HasBom { get; set; }

    public string? LineEnding { get; set; }

    /// <summary>Dosya açıldığındaki sürüm; boşsa kontrol yapılmaz.</summary>
    public string? Version { get; set; }

    /// <summary>Dosya başkası tarafından değiştirilmiş olsa bile üzerine yaz.</summary>
    public bool Force { get; set; }
}

namespace ServerManager.Application.DTOs.Files;

public sealed class FileContentDto
{
    public string Path { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool HasBom { get; init; }

    public string LineEnding { get; init; } = "LF";

    public long Size { get; init; }

    public DateTime LastWriteTime { get; init; }

    /// <summary>Kaydederken geri gönderilir; dosya bu arada değiştiyse kayıt reddedilir.</summary>
    public string Version { get; init; } = string.Empty;
}

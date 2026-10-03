using ServerManager.Application.Files;

namespace ServerManager.Application.DTOs.Files;

public sealed class FileEntryDto
{
    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public RemoteFileKind Kind { get; init; }

    public long Size { get; init; }

    public DateTime LastWriteTime { get; init; }

    public int Mode { get; init; }

    public string Permissions { get; init; } = string.Empty;

    public string OctalMode { get; init; } = string.Empty;

    public string Owner { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public bool IsHidden => Name.StartsWith('.');

    public bool IsDirectory => Kind == RemoteFileKind.Directory;
}

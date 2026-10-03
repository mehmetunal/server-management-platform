namespace ServerManager.Application.DTOs.Files;

public sealed class FileListingDto
{
    public string Path { get; init; } = "/";

    public string? ParentPath { get; init; }

    public string HomeDirectory { get; init; } = "/";

    public IReadOnlyList<FileBreadcrumbDto> Breadcrumbs { get; init; } = [];

    /// <summary>Önce klasörler, sonra ada göre sıralı.</summary>
    public IReadOnlyList<FileEntryDto> Entries { get; init; } = [];
}

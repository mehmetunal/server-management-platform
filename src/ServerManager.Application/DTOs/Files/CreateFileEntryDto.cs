namespace ServerManager.Application.DTOs.Files;

public sealed class CreateFileEntryDto
{
    public string Directory { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }
}

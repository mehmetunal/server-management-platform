namespace ServerManager.Application.DTOs.Files;

public sealed class UploadFileDto
{
    public string Directory { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public bool Overwrite { get; set; }
}

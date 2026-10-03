namespace ServerManager.Application.DTOs.Files;

public sealed class CopyFileDto
{
    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;
}

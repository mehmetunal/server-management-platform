namespace ServerManager.Application.DTOs.Docker;

public sealed class RenameContainerDto
{
    public string Container { get; set; } = string.Empty;

    public string NewName { get; set; } = string.Empty;
}

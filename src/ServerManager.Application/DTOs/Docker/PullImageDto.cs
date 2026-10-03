namespace ServerManager.Application.DTOs.Docker;

public sealed class PullImageDto
{
    public string Reference { get; set; } = string.Empty;
}

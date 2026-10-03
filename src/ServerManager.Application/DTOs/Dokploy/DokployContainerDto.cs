namespace ServerManager.Application.DTOs.Dokploy;

public sealed class DokployContainerDto
{
    public string Name { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool IsRunning => State == "running";
}

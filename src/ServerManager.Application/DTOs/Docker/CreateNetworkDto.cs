namespace ServerManager.Application.DTOs.Docker;

public sealed class CreateNetworkDto
{
    public string Name { get; set; } = string.Empty;

    public string Driver { get; set; } = "bridge";

    public string? Subnet { get; set; }

    public string? Gateway { get; set; }

    public bool Internal { get; set; }
}

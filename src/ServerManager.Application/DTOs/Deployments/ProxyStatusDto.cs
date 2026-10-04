namespace ServerManager.Application.DTOs.Deployments;

public sealed class ProxyStatusDto
{
    public bool Installed { get; init; }

    public bool Running { get; init; }

    public string Message { get; init; } = string.Empty;
}

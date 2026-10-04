using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class DomainListItemDto
{
    public Guid Id { get; init; }

    public string Host { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int ContainerPort { get; init; }

    public string? ServiceName { get; init; }

    public DeploymentTlsMode TlsMode { get; init; }

    public bool HasCertificate { get; init; }
}

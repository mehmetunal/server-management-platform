using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Deploy anında container'a yazılacak tek yönlendirme. Sertifika yalnızca bellekte durur.</summary>
public sealed class DeploymentRoute
{
    public required string RouterName { get; init; }

    public required string Host { get; init; }

    public string Path { get; init; } = string.Empty;

    public int ContainerPort { get; init; }

    public string? ServiceName { get; init; }

    public DeploymentTlsMode TlsMode { get; init; }

    public string? CertificatePem { get; init; }

    public string? PrivateKeyPem { get; init; }
}

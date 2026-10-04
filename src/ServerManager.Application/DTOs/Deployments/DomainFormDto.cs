using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class DomainFormDto
{
    public Guid? Id { get; set; }

    public string Host { get; set; } = string.Empty;

    public string? Path { get; set; }

    public int ContainerPort { get; set; } = 80;

    public string? ServiceName { get; set; }

    public DeploymentTlsMode TlsMode { get; set; } = DeploymentTlsMode.Cloudflare;

    public string? CertificatePem { get; set; }

    public string? PrivateKey { get; set; }

    public void ClearSecrets()
    {
        CertificatePem = null;
        PrivateKey = null;
    }
}

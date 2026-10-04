using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

/// <summary>Bir deployment projesinin host adını sunucudaki Traefik'e bağlar.</summary>
public class DeploymentDomain : BaseEntity
{
    public Guid ProjectId { get; set; }

    public DeploymentProject? Project { get; set; }

    public Guid ServerId { get; set; }

    public string Host { get; set; } = string.Empty;

    /// <summary>Boşsa host'un tamamı. Doluysa <c>PathPrefix</c>.</summary>
    public string Path { get; set; } = string.Empty;

    public int ContainerPort { get; set; }

    /// <summary>Compose servis adı. Dockerfile projesinde boştur.</summary>
    public string? ServiceName { get; set; }

    public DeploymentTlsMode TlsMode { get; set; }

    public string? EncryptedCertificate { get; set; }

    public string? EncryptedPrivateKey { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}

using ServerManager.Domain.Common;

namespace ServerManager.Plugin.DevOps.Dokploy.Domain;

/// <summary>Bir sunucudaki Dokploy kurulumunun panel tarafındaki kaydı (sunucu başına en fazla bir tane).</summary>
public class DokployInstance : BaseEntity
{
    public Guid ServerId { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string? EncryptedApiKey { get; set; }

    public string? Version { get; set; }

    public DokployStatus Status { get; set; } = DokployStatus.Unknown;

    public string? StatusMessage { get; set; }

    public DateTime? LastHealthCheckAt { get; set; }

    public int? LastResponseTimeMs { get; set; }

    public DateTime? InstalledAt { get; set; }

    public Guid? InstallationId { get; set; }
}

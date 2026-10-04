using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class Server : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int SshPort { get; set; } = 22;

    public string Username { get; set; } = string.Empty;

    public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.Password;

    public bool UseSudo { get; set; }

    public string? Description { get; set; }

    public ServerEnvironment Environment { get; set; } = ServerEnvironment.Production;

    public string? Location { get; set; }

    public string? Provider { get; set; }

    public string? OperatingSystem { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public string? HostKeyFingerprint { get; set; }

    public DateTime? LastConnectionTestAt { get; set; }

    public bool? LastConnectionSucceeded { get; set; }

    public string? LastConnectionMessage { get; set; }

    public bool MonitoringEnabled { get; set; } = true;

    public DateTime? LastSeenAt { get; set; }

    public int ConsecutiveFailureCount { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Guid? GroupId { get; set; }

    public ServerGroup? Group { get; set; }

    public decimal? MonthlyCost { get; set; }

    public string? CostCurrency { get; set; }

    public Guid? CloudAccountId { get; set; }

    public CloudAccount? CloudAccount { get; set; }

    /// <summary>Sağlayıcıdaki sunucu kimliği; <see cref="CloudAccountId"/> ile birlikte dolu olur.</summary>
    public string? CloudExternalId { get; set; }

    public ServerCredential? Credential { get; set; }

    public ICollection<ServerTag> Tags { get; set; } = new List<ServerTag>();
}

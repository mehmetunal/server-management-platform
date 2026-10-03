using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Servers;

public sealed class ServerDetailsDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Hostname { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public int SshPort { get; init; }

    public string Username { get; init; } = string.Empty;

    public AuthenticationType AuthenticationType { get; init; }

    public bool UseSudo { get; init; }

    public string? Description { get; init; }

    public ServerEnvironment Environment { get; init; }

    public string? Location { get; init; }

    public string? Provider { get; init; }

    public string? OperatingSystem { get; init; }

    public ServerStatus Status { get; init; }

    public string? HostKeyFingerprint { get; init; }

    public DateTime? LastConnectionTestAt { get; init; }

    public bool? LastConnectionSucceeded { get; init; }

    public string? LastConnectionMessage { get; init; }

    public bool HasPassword { get; init; }

    public bool HasPrivateKey { get; init; }

    public bool HasPassphrase { get; init; }

    public bool HasSudoPassword { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public DateTime CreatedAt { get; init; }

    public string? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}

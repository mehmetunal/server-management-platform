using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Servers;

public sealed class ServerListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Hostname { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public int SshPort { get; init; }

    public ServerEnvironment Environment { get; init; }

    public ServerStatus Status { get; init; }

    public string? OperatingSystem { get; init; }

    public string? Provider { get; init; }

    public string? Location { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public DateTime? LastConnectionTestAt { get; init; }

    public bool? LastConnectionSucceeded { get; init; }
}

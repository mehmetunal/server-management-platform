using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Mappings;

public static class ServerMappings
{
    public static ServerListItemDto ToListItemDto(this Server server) => new()
    {
        Id = server.Id,
        Name = server.Name,
        Hostname = server.Hostname,
        IpAddress = server.IpAddress,
        SshPort = server.SshPort,
        Environment = server.Environment,
        Status = server.Status,
        OperatingSystem = server.OperatingSystem,
        Provider = server.Provider,
        Location = server.Location,
        Tags = server.Tags.Select(t => t.Name).OrderBy(t => t).ToList(),
        LastConnectionTestAt = server.LastConnectionTestAt,
        LastConnectionSucceeded = server.LastConnectionSucceeded
    };

    public static ServerDetailsDto ToDetailsDto(this Server server) => new()
    {
        Id = server.Id,
        Name = server.Name,
        Hostname = server.Hostname,
        IpAddress = server.IpAddress,
        SshPort = server.SshPort,
        Username = server.Username,
        AuthenticationType = server.AuthenticationType,
        UseSudo = server.UseSudo,
        MonitoringEnabled = server.MonitoringEnabled,
        LastSeenAt = server.LastSeenAt,
        Description = server.Description,
        Environment = server.Environment,
        Location = server.Location,
        Provider = server.Provider,
        OperatingSystem = server.OperatingSystem,
        Status = server.Status,
        HostKeyFingerprint = server.HostKeyFingerprint,
        LastConnectionTestAt = server.LastConnectionTestAt,
        LastConnectionSucceeded = server.LastConnectionSucceeded,
        LastConnectionMessage = server.LastConnectionMessage,
        HasPassword = server.Credential?.EncryptedPassword is not null,
        HasPrivateKey = server.Credential?.EncryptedPrivateKey is not null,
        HasPassphrase = server.Credential?.EncryptedPassphrase is not null,
        HasSudoPassword = server.Credential?.EncryptedSudoPassword is not null,
        Tags = server.Tags.Select(t => t.Name).OrderBy(t => t).ToList(),
        CreatedAt = server.CreatedAt,
        CreatedBy = server.CreatedBy,
        UpdatedAt = server.UpdatedAt,
        UpdatedBy = server.UpdatedBy
    };

    public static UpdateServerDto ToUpdateDto(this Server server) => new()
    {
        Id = server.Id,
        Name = server.Name,
        Hostname = server.Hostname,
        IpAddress = server.IpAddress,
        SshPort = server.SshPort,
        Username = server.Username,
        AuthenticationType = server.AuthenticationType,
        UseSudo = server.UseSudo,
        MonitoringEnabled = server.MonitoringEnabled,
        Description = server.Description,
        Environment = server.Environment,
        Location = server.Location,
        Provider = server.Provider,
        OperatingSystem = server.OperatingSystem,
        Tags = TagParser.Join(server.Tags.Select(t => t.Name).OrderBy(t => t)),
        HasPassword = server.Credential?.EncryptedPassword is not null,
        HasPrivateKey = server.Credential?.EncryptedPrivateKey is not null,
        HasPassphrase = server.Credential?.EncryptedPassphrase is not null,
        HasSudoPassword = server.Credential?.EncryptedSudoPassword is not null
    };
}

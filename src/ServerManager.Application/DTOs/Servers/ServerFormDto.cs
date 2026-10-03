using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Servers;

public abstract class ServerFormDto
{
    public string Name { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int SshPort { get; set; } = 22;

    public string Username { get; set; } = string.Empty;

    public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.Password;

    public bool UseSudo { get; set; }

    public bool MonitoringEnabled { get; set; } = true;

    public string? Password { get; set; }

    public string? PrivateKey { get; set; }

    public string? Passphrase { get; set; }

    public string? SudoPassword { get; set; }

    public string? Description { get; set; }

    public ServerEnvironment Environment { get; set; } = ServerEnvironment.Production;

    public string? Location { get; set; }

    public string? Provider { get; set; }

    public string? OperatingSystem { get; set; }

    public string? Tags { get; set; }

    public void ClearSecrets()
    {
        Password = null;
        PrivateKey = null;
        Passphrase = null;
        SudoPassword = null;
    }
}

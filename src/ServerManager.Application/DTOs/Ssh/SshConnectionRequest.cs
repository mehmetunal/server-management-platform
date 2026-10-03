using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Ssh;

public sealed class SshConnectionRequest
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 22;

    public string Username { get; init; } = string.Empty;

    public AuthenticationType AuthenticationType { get; init; }

    public string? Password { get; init; }

    public string? PrivateKey { get; init; }

    public string? Passphrase { get; init; }

    public string? ExpectedHostKeyFingerprint { get; init; }

    public override string ToString() => $"{Username}@{Host}:{Port}";
}

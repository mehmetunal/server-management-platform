using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Servers;

public sealed class ConnectionTestResultDto
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? HostKeyFingerprint { get; init; }

    public bool FingerprintTrustedNow { get; init; }

    public bool FingerprintMismatch { get; init; }

    public long DurationMs { get; init; }

    public DateTime TestedAt { get; init; }

    public ServerStatus Status { get; init; }
}

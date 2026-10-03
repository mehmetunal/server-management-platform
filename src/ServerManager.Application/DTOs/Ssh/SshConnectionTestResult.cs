namespace ServerManager.Application.DTOs.Ssh;

public sealed class SshConnectionTestResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? HostKeyFingerprint { get; init; }

    public bool FingerprintMismatch { get; init; }

    public string? OperatingSystem { get; init; }

    public long DurationMs { get; init; }
}

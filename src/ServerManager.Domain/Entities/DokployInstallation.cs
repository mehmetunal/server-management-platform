using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class DokployInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public string? RequestedVersion { get; set; }

    public string ScriptUrl { get; set; } = string.Empty;

    public string? ScriptSha256 { get; set; }

    public DokployInstallationStatus Status { get; set; } = DokployInstallationStatus.Running;

    public int? ExitCode { get; set; }

    public string? FailureReason { get; set; }

    public string Output { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

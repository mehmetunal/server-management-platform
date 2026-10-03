using ServerManager.Plugin.DevOps.Dokploy.Domain;

namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed class DokployInstallationDto
{
    public Guid Id { get; init; }

    public string? UserName { get; init; }

    public string? RequestedVersion { get; init; }

    public string ScriptUrl { get; init; } = string.Empty;

    public string? ScriptSha256 { get; init; }

    public DokployInstallationStatus Status { get; init; }

    public int? ExitCode { get; init; }

    public string? FailureReason { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    /// <summary>Yalnızca tek kurulum istendiğinde doldurulur.</summary>
    public string? Output { get; init; }
}

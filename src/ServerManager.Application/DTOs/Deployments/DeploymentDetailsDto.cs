using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class DeploymentDetailsDto : DeploymentListItemDto
{
    public DeploymentBuildType BuildType { get; init; }

    public string? RequestedCommit { get; init; }

    public string? CommitAuthor { get; init; }

    public string? CommitUrl { get; init; }

    public Guid? SourceDeploymentId { get; init; }

    public int? ExitCode { get; init; }

    public string? IpAddress { get; init; }

    public DateTime? BuildStartedAt { get; init; }

    public DateTime? DeployStartedAt { get; init; }

    public string? CancelledBy { get; init; }

    public string Log { get; init; } = string.Empty;

    /// <summary>Proje silinmemişse yeniden dağıtılabilir.</summary>
    public bool ProjectExists { get; init; }
}

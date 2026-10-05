using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class Deployment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public DeploymentBuildType BuildType { get; set; }

    public string Branch { get; set; } = string.Empty;

    /// <summary>Kullanıcının istediği commit; boşsa dalın son commit'i dağıtılır.</summary>
    public string? RequestedCommit { get; set; }

    public string? CommitSha { get; set; }

    public string? CommitMessage { get; set; }

    public string? CommitAuthor { get; set; }

    /// <summary>Yeniden deploy ve geri dönüşte kaynak alınan deployment; geri dönüşte hedef sürümdür.</summary>
    public Guid? SourceDeploymentId { get; set; }

    public DeploymentKind Kind { get; set; } = DeploymentKind.Deploy;

    public DeploymentStatus Status { get; set; } = DeploymentStatus.Started;

    public string? FailureReason { get; set; }

    public int? ExitCode { get; set; }

    public string Log { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? BuildStartedAt { get; set; }

    public DateTime? DeployStartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CancelledBy { get; set; }
}

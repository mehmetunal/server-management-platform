namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupRunDetailsDto : BackupRunListItemDto
{
    public string? Sha256 { get; init; }

    public Guid? SourceRunId { get; init; }

    public string? CancelledBy { get; init; }

    public string? ArtifactDeletedBy { get; init; }

    public string Log { get; init; } = string.Empty;
}

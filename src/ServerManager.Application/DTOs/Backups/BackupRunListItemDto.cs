using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

public class BackupRunListItemDto
{
    public Guid Id { get; init; }

    public BackupOperation Operation { get; init; }

    public BackupTrigger Trigger { get; init; }

    public BackupRunStatus Status { get; init; }

    public Guid? JobId { get; init; }

    public string JobName { get; init; } = string.Empty;

    public BackupSourceType SourceType { get; init; }

    public Guid ServerId { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string StorageName { get; init; } = string.Empty;

    public string? FileName { get; init; }

    public long? SizeBytes { get; init; }

    public bool IsEncrypted { get; init; }

    public string? RestoreTarget { get; init; }

    public string? FailureReason { get; init; }

    public string? UserName { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? ArtifactDeletedAt { get; init; }

    /// <summary>Başarılı bir yedek ve dosyası silinmemiş: indirilebilir ve geri yüklenebilir.</summary>
    public bool IsArtifactAvailable { get; init; }

    public bool IsRunning => Status == BackupRunStatus.Running;
}

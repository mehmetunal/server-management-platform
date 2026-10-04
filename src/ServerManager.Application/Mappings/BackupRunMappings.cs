using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Mappings;

public static class BackupRunMappings
{
    public static bool IsArtifactAvailable(this BackupRun run) =>
        run.Operation == BackupOperation.Backup
        && run.Status == BackupRunStatus.Succeeded
        && run.ArtifactDeletedAt is null
        && !string.IsNullOrEmpty(run.ObjectKey);

    public static BackupRunListItemDto ToListItemDto(this BackupRun run) => new()
    {
        Id = run.Id,
        Operation = run.Operation,
        Trigger = run.Trigger,
        Status = run.Status,
        JobId = run.JobId,
        JobName = run.JobName,
        SourceType = run.SourceType,
        ServerId = run.ServerId,
        ServerName = run.ServerName,
        StorageName = run.StorageName,
        FileName = run.FileName,
        SizeBytes = run.SizeBytes,
        IsEncrypted = run.IsEncrypted,
        RestoreTarget = run.RestoreTarget,
        FailureReason = run.FailureReason,
        UserName = run.UserName,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        ArtifactDeletedAt = run.ArtifactDeletedAt,
        IsArtifactAvailable = run.IsArtifactAvailable()
    };

    public static BackupRunDetailsDto ToDetailsDto(this BackupRun run, bool includeLog) => new()
    {
        Id = run.Id,
        Operation = run.Operation,
        Trigger = run.Trigger,
        Status = run.Status,
        JobId = run.JobId,
        JobName = run.JobName,
        SourceType = run.SourceType,
        ServerId = run.ServerId,
        ServerName = run.ServerName,
        StorageName = run.StorageName,
        FileName = run.FileName,
        SizeBytes = run.SizeBytes,
        IsEncrypted = run.IsEncrypted,
        RestoreTarget = run.RestoreTarget,
        FailureReason = run.FailureReason,
        UserName = run.UserName,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        ArtifactDeletedAt = run.ArtifactDeletedAt,
        IsArtifactAvailable = run.IsArtifactAvailable(),
        Sha256 = run.Sha256,
        SourceRunId = run.SourceRunId,
        CancelledBy = run.CancelledBy,
        ArtifactDeletedBy = run.ArtifactDeletedBy,
        Log = includeLog ? run.Log : string.Empty
    };
}

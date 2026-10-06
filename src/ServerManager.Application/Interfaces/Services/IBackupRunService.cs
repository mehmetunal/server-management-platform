using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Interfaces.Services;

public interface IBackupRunService
{
    Task<PagedResult<BackupRunListItemDto>> SearchAsync(BackupRunFilterDto filter, CancellationToken cancellationToken = default);

    Task<ServiceResult<BackupRunDetailsDto>> GetAsync(Guid id, bool includeLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupRunListItemDto>> GetRecentAsync(Guid jobId, int count, CancellationToken cancellationToken = default);

    /// <summary>Çalışma kaydını oluşturur; asıl iş <see cref="RunBackupAsync"/> ile arka planda yürür.</summary>
    Task<ServiceResult<Guid>> BeginBackupAsync(Guid jobId, BackupTrigger trigger, BackupActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult> RunBackupAsync(Guid runId, BackupActor actor, BackupCancellation cancellation);

    Task<ServiceResult<BackupRestoreFormDto>> GetRestoreFormAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> BeginRestoreAsync(BackupRestoreDto dto, BackupActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult> RunRestoreAsync(Guid restoreRunId, BackupRestoreDto dto, BackupActor actor, BackupCancellation cancellation);

    Task<ServiceResult> DeleteArtifactAsync(Guid runId, BackupActor actor, CancellationToken cancellationToken = default);

    Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default);
}

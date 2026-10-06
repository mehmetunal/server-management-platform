using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IBackupRepository
{
    Task<IReadOnlyList<BackupStorage>> GetStoragesAsync(CancellationToken cancellationToken = default);

    Task<BackupStorage?> GetStorageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Silinmiş depolama da döner; eski yedekleri geri yüklemek ve indirmek için.</summary>
    Task<BackupStorage?> GetStorageIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> StorageNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetJobCountsByStorageAsync(CancellationToken cancellationToken = default);

    Task AddStorageAsync(BackupStorage storage, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupJob>> GetJobsAsync(Guid? serverId, CancellationToken cancellationToken = default);

    Task<BackupJob?> GetJobAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Silinmiş iş de döner; eski yedeğin veritabanı bilgileri için.</summary>
    Task<BackupJob?> GetJobIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> JobNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddJobAsync(BackupJob job, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupJob>> GetDueJobsAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);

    Task AddRunAsync(BackupRun run, CancellationToken cancellationToken = default);

    Task<BackupRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<BackupRun> Items, int TotalCount)> SearchRunsAsync(BackupRunFilterDto filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> HasRunningBackupAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupRun>> GetRecentRunsAsync(Guid jobId, int count, CancellationToken cancellationToken = default);

    /// <summary>İşin dosyası silinmemiş başarılı yedekleri (saklama politikası için).</summary>
    Task<IReadOnlyList<BackupRun>> GetAvailableBackupsAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Her iş için dosyası silinmemiş en yeni başarılı yedek; yedeği olmayan iş sözlükte yer almaz.</summary>
    Task<IReadOnlyDictionary<Guid, BackupArtifactRef>> GetLatestAvailableBackupsAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default);

    Task UpdateRunLogAsync(Guid id, string log, CancellationToken cancellationToken = default);

    Task<int> InterruptRunningAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

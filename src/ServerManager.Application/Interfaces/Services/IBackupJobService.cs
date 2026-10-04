using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.Interfaces.Services;

public interface IBackupJobService
{
    Task<IReadOnlyList<BackupJobListItemDto>> GetJobsAsync(Guid? serverId = null, CancellationToken cancellationToken = default);

    Task<ServiceResult<BackupJobDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<BackupJobFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(BackupJobFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(BackupJobFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerOptionDto>> GetServerOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Zamanı gelen işlerin bir sonraki çalışma zamanını ileri alır ve kimliklerini döner (aynı iş iki kez başlatılmaz).</summary>
    Task<IReadOnlyList<Guid>> ClaimDueJobsAsync(int take, CancellationToken cancellationToken = default);
}

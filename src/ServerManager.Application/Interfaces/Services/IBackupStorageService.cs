using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Services;

public interface IBackupStorageService
{
    Task<IReadOnlyList<BackupStorageListItemDto>> GetStoragesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupStorageOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<BackupStorageProviderDto> GetProviders();

    Task<ServiceResult<BackupStorageFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(BackupStorageFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(BackupStorageFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult> TestAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ayarları çözer ve sağlayıcıyı bulur; eklenti devre dışıysa veya ayarlar çözülemezse hata döner.</summary>
    ServiceResult<BackupStorageTarget> Resolve(BackupStorage storage);
}

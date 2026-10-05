using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.ManagedServices;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>Veritabanı servisleri için yedekleme işi (yetki kontrolü çağıran katmanda: backup.manage).</summary>
public interface IManagedServiceBackupService
{
    /// <summary>Servisin container'ını yedekleyen işler (aynı sunucu + container adı).</summary>
    Task<IReadOnlyList<BackupJobListItemDto>> ListJobsAsync(Guid serviceId, CancellationToken cancellationToken = default);

    /// <summary>Servisin kimlik bilgileriyle <see cref="IBackupJobService.CreateForContainerDatabaseAsync"/> çağırır.</summary>
    Task<ServiceResult<Guid>> CreateJobAsync(Guid serviceId, ServiceBackupOptionsDto options, CancellationToken cancellationToken = default);

    /// <summary>Kurulumdan önce form denetimi (motor desteği, depolama, zorunlu veritabanı adı); servis henüz yokken çalışır.</summary>
    Task<ServiceResult> ValidateOptionsAsync(string? templateKey, string? serviceDatabase, ServiceBackupOptionsDto options, CancellationToken cancellationToken = default);
}

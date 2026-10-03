using ServerManager.Application.Common;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Services;

public interface IDokployService
{
    /// <summary>Kayıtlı durum, sunucudaki canlı durum ve kurulum geçmişi. Panel dışında kurulmuş Dokploy burada kaydedilir.</summary>
    Task<ServiceResult<DokployOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<DokployCompatibilityReportDto>> CheckCompatibilityAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<DokployHealthResultDto>> RunHealthCheckAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DokployProjectDto>>> GetProjectsAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> SaveSettingsAsync(Guid serverId, DokploySettingsDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveApiKeyAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>Uyumluluğu yeniden doğrular ve kurulum kaydını açar; asıl kurulum <see cref="RunInstallationAsync"/> ile arka planda yapılır.</summary>
    Task<ServiceResult<Guid>> BeginInstallationAsync(Guid serverId, DokployInstallRequestDto dto, DokployActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult> RunInstallationAsync(Guid installationId, DokployActor actor, IDokployInstallObserver observer, CancellationToken cancellationToken = default);

    /// <summary>Sunucuya bağlanmadan yalnız kayıtlardan devam eden kurulumu bulur.</summary>
    Task<Guid?> GetRunningInstallationIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<DokployInstallationDto>> GetInstallationAsync(Guid serverId, Guid installationId, CancellationToken cancellationToken = default);

    Task<int> InterruptRunningInstallationsAsync(CancellationToken cancellationToken = default);

    Task RunScheduledHealthChecksAsync(CancellationToken cancellationToken = default);
}

using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Plugin.DevOps.Dokploy.Domain;

namespace ServerManager.Plugin.DevOps.Dokploy.Services;

public interface IDokployRepository : IRepository<DokployInstance>
{
    Task<DokployInstance?> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>Son kurulumlar (en yeni önce); liste için çıktı alanı yüklenmez.</summary>
    Task<IReadOnlyList<DokployInstallation>> GetInstallationsAsync(Guid serverId, int count, CancellationToken cancellationToken = default);

    Task<DokployInstallation?> GetInstallationAsync(Guid installationId, CancellationToken cancellationToken = default);

    Task<DokployInstallation?> GetRunningInstallationAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task AddInstallationAsync(DokployInstallation installation, CancellationToken cancellationToken = default);

    Task UpdateInstallationOutputAsync(Guid installationId, string output, CancellationToken cancellationToken = default);

    /// <summary>Uygulama kapanırken yarım kalmış kurulumları kesildi olarak işaretler; kayıt silinmez.</summary>
    Task<int> InterruptRunningInstallationsAsync(DateTime completedAt, string reason, CancellationToken cancellationToken = default);

    /// <summary>Silinmemiş ve host key'i doğrulanmış sunuculardaki Dokploy kayıtlarının sunucu kimlikleri.</summary>
    Task<IReadOnlyList<Guid>> GetServerIdsForHealthCheckAsync(CancellationToken cancellationToken = default);
}

using ServerManager.Application.Common;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Application.Interfaces.Services;

public interface IResourceUsageService
{
    /// <param name="includeDocker">Kullanıcının Docker görüntüleme yetkisi yoksa container istatistikleri okunmaz.</param>
    Task<ServiceResult<ResourceOverview>> GetOverviewAsync(Guid serverId, bool includeDocker, CancellationToken cancellationToken = default);

    Task<ServiceResult<DiskUsageReport>> ScanDiskAsync(Guid serverId, string? path, CancellationToken cancellationToken = default);
}

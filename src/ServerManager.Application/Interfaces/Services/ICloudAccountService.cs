using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Cloud;

namespace ServerManager.Application.Interfaces.Services;

public interface ICloudAccountService
{
    IReadOnlyList<CloudProviderOptionDto> GetProviders();

    Task<IReadOnlyList<CloudAccountListItemDto>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CloudAccountOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<CloudAccountFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(CloudAccountFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(CloudAccountFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<CloudAccountServersDto>> GetServersAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Bağlı sunucuların maliyetini günceller, IP'si eşleşen bağlantısız sunucuları hesaba bağlar.</summary>
    Task<ServiceResult<CloudSyncSummary>> SyncAsync(Guid id, bool automatic = false, CancellationToken cancellationToken = default);

    Task<int> SyncAllAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<CloudCatalog>> GetCatalogAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<CloudProvisionResultDto>> ProvisionAsync(CloudProvisionDto dto, CancellationToken cancellationToken = default);
}

using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.Interfaces.Services;

public interface IServerService
{
    Task<PagedResult<ServerListItemDto>> SearchAsync(ServerFilterDto filter, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServerDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Yumuşak silinmiş sunucunun adı. Denetim geçmişindeki bağlantı 404 vermesin diye.</summary>
    Task<string?> GetRemovedNameAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<UpdateServerDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(CreateServerDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(UpdateServerDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, string? confirmationName, CancellationToken cancellationToken = default);

    Task<ServiceResult<ConnectionTestResultDto>> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetTagNamesAsync(CancellationToken cancellationToken = default);
}

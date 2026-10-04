using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ServerGroups;

namespace ServerManager.Application.Interfaces.Services;

public interface IServerGroupService
{
    Task<IReadOnlyList<ServerGroupListItemDto>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerGroupOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<ServerGroupFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(ServerGroupFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(ServerGroupFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

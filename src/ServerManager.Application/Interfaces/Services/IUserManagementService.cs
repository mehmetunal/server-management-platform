using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Users;

namespace ServerManager.Application.Interfaces.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<UpdateUserDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(UpdateUserDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> SetLockAsync(Guid id, bool locked, CancellationToken cancellationToken = default);
}

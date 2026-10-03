using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Account;

namespace ServerManager.Application.Interfaces.Services;

public interface IAccountService
{
    Task<ServiceResult> SignInAsync(LoginDto dto, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

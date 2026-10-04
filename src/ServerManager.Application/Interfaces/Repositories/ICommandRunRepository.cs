using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Commands;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface ICommandRunRepository
{
    Task<PagedResult<CommandRun>> SearchAsync(CommandRunFilterDto filter, CancellationToken cancellationToken = default);

    Task<CommandRun?> GetWithTargetsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommandRun>> GetRunningAsync(CancellationToken cancellationToken = default);

    Task AddAsync(CommandRun run, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

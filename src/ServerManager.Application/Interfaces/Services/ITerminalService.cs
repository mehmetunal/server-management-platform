using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Application.Interfaces.Services;

public interface ITerminalService
{
    Task<ServiceResult<TerminalHandle>> OpenServerShellAsync(
        Guid serverId,
        int columns,
        int rows,
        TerminalActor actor,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TerminalHandle>> OpenContainerShellAsync(
        Guid serverId,
        string container,
        int columns,
        int rows,
        TerminalActor actor,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);

    Task CompleteSessionAsync(TerminalHandle handle, TerminalActor actor, string? reason, CancellationToken cancellationToken = default);

    Task RecordCommandsAsync(IReadOnlyList<TerminalCommandEntry> entries, CancellationToken cancellationToken = default);

    Task<int> CloseOpenSessionsAsync(DateTime startedBefore, string reason, CancellationToken cancellationToken = default);

    /// <param name="userId">null değilse yalnızca bu kullanıcının oturumları döner.</param>
    Task<PagedResult<TerminalSessionDto>> GetSessionsAsync(Guid serverId, string? userId, int page, CancellationToken cancellationToken = default);

    /// <param name="userId">null değilse oturum yalnızca bu kullanıcıya aitse döner.</param>
    Task<ServiceResult<TerminalSessionDetailsDto>> GetSessionAsync(Guid serverId, Guid sessionId, string? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRecentCommandsAsync(Guid serverId, string userId, CancellationToken cancellationToken = default);
}

using ServerManager.Application.Common;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface ITerminalLogRepository : IRepository<TerminalSessionLog>
{
    Task<PagedResult<TerminalSessionLog>> GetSessionsAsync(Guid serverId, string? userId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TerminalCommandLog>> GetCommandsAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Kullanıcının bu sunucuda en son çalıştırdığı, tam metni bilinen farklı komutlar (en yeni önce).</summary>
    Task<IReadOnlyList<string>> GetRecentCommandTextsAsync(Guid serverId, string userId, int count, CancellationToken cancellationToken = default);

    Task AddCommandsAsync(IEnumerable<TerminalCommandLog> commands, CancellationToken cancellationToken = default);

    Task IncrementCommandCountsAsync(IReadOnlyDictionary<Guid, int> countsBySession, CancellationToken cancellationToken = default);

    Task CloseSessionAsync(Guid sessionId, DateTime endedAt, string? closeReason, CancellationToken cancellationToken = default);

    /// <summary>Uygulama kapanırken açık kalmış oturumları kapanmış olarak işaretler; kayıt silinmez.</summary>
    Task<int> CloseOpenSessionsAsync(DateTime startedBefore, DateTime endedAt, string closeReason, CancellationToken cancellationToken = default);
}

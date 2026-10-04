using ServerManager.Application.Common;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Interfaces.Services;

public interface IServerSystemService
{
    Task<ServiceResult<ServiceList>> GetServicesAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> ControlServiceAsync(Guid serverId, ServiceManagerKind manager, string name, ServiceAction action, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProcessList>> GetProcessesAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> SignalProcessAsync(Guid serverId, int pid, ProcessSignal signal, string? processName, CancellationToken cancellationToken = default);

    Task<ServiceResult<LogSnapshot>> GetLogsAsync(Guid serverId, LogRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<NetworkSnapshot>> GetNetworkAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<StorageSnapshot>> GetStorageAsync(Guid serverId, CancellationToken cancellationToken = default);
}

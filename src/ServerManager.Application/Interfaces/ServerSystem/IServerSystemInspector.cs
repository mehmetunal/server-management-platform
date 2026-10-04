using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Interfaces.ServerSystem;

/// <summary>Sunucunun servis, process, log, ağ ve disk bilgisini SSH üzerinden okur; yalnızca servis/process işlemleri değişiklik yapar.</summary>
public interface IServerSystemInspector
{
    Task<ServiceResult<ServiceList>> GetServicesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> ControlServiceAsync(RemoteExecutionContext context, ServiceManagerKind manager, string name, ServiceAction action, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProcessList>> GetProcessesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> SignalProcessAsync(RemoteExecutionContext context, int pid, ProcessSignal signal, CancellationToken cancellationToken = default);

    Task<ServiceResult<LogSnapshot>> GetLogsAsync(RemoteExecutionContext context, LogRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<NetworkSnapshot>> GetNetworkAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult<StorageSnapshot>> GetStorageAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);
}

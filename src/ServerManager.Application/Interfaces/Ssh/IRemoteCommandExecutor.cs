using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Ssh;

public interface IRemoteCommandExecutor
{
    Task<RemoteCommandOutput> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default);
}

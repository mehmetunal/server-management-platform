using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Application.Interfaces.Docker;

public interface IDockerClient
{
    Task<ServiceResult<DockerOverviewDto>> GetOverviewAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerContainerDto>>> GetContainersAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerContainerStatsDto>>> GetContainerStatsAsync(RemoteExecutionContext context, string? container, CancellationToken cancellationToken = default);

    Task<ServiceResult<DockerContainerDetailsDto>> GetContainerAsync(RemoteExecutionContext context, string container, CancellationToken cancellationToken = default);

    Task<ServiceResult<DockerLogsDto>> GetContainerLogsAsync(RemoteExecutionContext context, string container, int tail, string? since, CancellationToken cancellationToken = default);

    Task<ServiceResult> ExecuteContainerActionAsync(RemoteExecutionContext context, string container, DockerContainerAction action, bool force, CancellationToken cancellationToken = default);

    Task<ServiceResult> RenameContainerAsync(RemoteExecutionContext context, string container, string newName, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerImageDto>>> GetImagesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult> PullImageAsync(RemoteExecutionContext context, string reference, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveImageAsync(RemoteExecutionContext context, string reference, bool force, CancellationToken cancellationToken = default);

    Task<ServiceResult> PruneImagesAsync(RemoteExecutionContext context, bool all, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerVolumeDto>>> GetVolumesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateVolumeAsync(RemoteExecutionContext context, string name, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveVolumeAsync(RemoteExecutionContext context, string name, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerNetworkDto>>> GetNetworksAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateNetworkAsync(RemoteExecutionContext context, CreateNetworkDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveNetworkAsync(RemoteExecutionContext context, string network, CancellationToken cancellationToken = default);

    Task<ServiceResult<ITerminalSession>> OpenContainerTerminalAsync(
        RemoteExecutionContext context,
        string container,
        int columns,
        int rows,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);
}

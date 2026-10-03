using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Application.Interfaces.Services;

public interface IDockerService
{
    Task<ServiceResult<DockerOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerContainerDto>>> GetContainersAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerContainerStatsDto>>> GetContainerStatsAsync(Guid serverId, string? container, CancellationToken cancellationToken = default);

    Task<ServiceResult<DockerContainerDetailsDto>> GetContainerAsync(Guid serverId, string container, CancellationToken cancellationToken = default);

    Task<ServiceResult<DockerLogsDto>> GetContainerLogsAsync(Guid serverId, DockerLogQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult> ExecuteContainerActionAsync(Guid serverId, ContainerActionRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> RenameContainerAsync(Guid serverId, RenameContainerDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerImageDto>>> GetImagesAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> PullImageAsync(Guid serverId, PullImageDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveImageAsync(Guid serverId, string reference, bool force, CancellationToken cancellationToken = default);

    Task<ServiceResult> PruneImagesAsync(Guid serverId, bool all, string? confirmationName, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerVolumeDto>>> GetVolumesAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateVolumeAsync(Guid serverId, CreateVolumeDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveVolumeAsync(Guid serverId, string name, string? confirmationName, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<DockerNetworkDto>>> GetNetworksAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateNetworkAsync(Guid serverId, CreateNetworkDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveNetworkAsync(Guid serverId, string network, CancellationToken cancellationToken = default);

    Task<ServiceResult<ContainerTerminalHandle>> OpenTerminalAsync(
        Guid serverId,
        string container,
        int columns,
        int rows,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);

    Task LogTerminalClosedAsync(ContainerTerminalHandle handle, string? userName, CancellationToken cancellationToken = default);
}

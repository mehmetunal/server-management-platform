using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>
/// Projenin sunucuda çalışan container'ları ve logları (docker logs). Compose projelerinde
/// <c>com.docker.compose.project=sm-&lt;slug&gt;</c> etiketli, Dockerfile projelerinde <c>sm-&lt;slug&gt;</c> adlı container'lar.
/// </summary>
public interface IProjectRuntimeService
{
    Task<ServiceResult<IReadOnlyList<ProjectContainerDto>>> GetContainersAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>İstenen container'ın projeye ait olduğu doğrulanır; değilse NotFound döner.</summary>
    Task<ServiceResult<ProjectLogsDto>> GetLogsAsync(Guid projectId, ProjectLogQuery query, CancellationToken cancellationToken = default);
}

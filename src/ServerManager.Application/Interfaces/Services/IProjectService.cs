using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Application.Interfaces.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectListItemDto>> SearchAsync(ProjectFilterDto filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectListItemDto>> GetByServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProjectDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<UpdateProjectDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerOptionDto>> GetServerOptionsAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(CreateProjectDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(UpdateProjectDto dto, CancellationToken cancellationToken = default);

    /// <summary>Kaydı soft delete ile kaldırır; sunucudaki klasöre, container'lara ve deployment geçmişine dokunulmaz.</summary>
    Task<ServiceResult> DeleteAsync(Guid id, string? confirmationName, CancellationToken cancellationToken = default);

    Task<ServiceResult<GitBranchListDto>> ListBranchesAsync(Guid id, CancellationToken cancellationToken = default);
}

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

    /// <summary>Etkin Git entegrasyonlarındaki bağlantılar (GitHub App kurulumları vb.).</summary>
    Task<GitSourceListDto> GetGitSourcesAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GitRepositoryDto>>> ListGitRepositoriesAsync(string? sourceKey, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<string>>> ListGitBranchesAsync(string? sourceKey, string? repository, CancellationToken cancellationToken = default);

    /// <summary>Henüz kaydedilmemiş bir depo adresindeki dalları hedef sunucudan (git ls-remote) okur.</summary>
    Task<ServiceResult<IReadOnlyList<string>>> ListRemoteBranchesAsync(RemoteBranchQueryDto dto, CancellationToken cancellationToken = default);
}

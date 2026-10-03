using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Plugin.Git.GitHub.Domain;

namespace ServerManager.Plugin.Git.GitHub.Services;

public interface IGitHubAppRepository : IRepository<GitHubApp>
{
    /// <summary>Silinmemiş uygulamalar, ada göre.</summary>
    Task<IReadOnlyList<GitHubApp>> ListAsync(CancellationToken cancellationToken = default);

    Task<GitHubApp?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByAppIdAsync(long appId, CancellationToken cancellationToken = default);

    /// <summary>Bu uygulamanın kurulumlarından birini kullanan silinmemiş projeler.</summary>
    Task<IReadOnlyList<string>> GetProjectNamesAsync(Guid appRecordId, CancellationToken cancellationToken = default);
}

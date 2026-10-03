using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.Domain;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Services;

/// <summary>Kayıtlı bir App adına GitHub çağrıları: JWT üretimi, kurulum anahtarları ve kısa süreli liste önbelleği.</summary>
public interface IGitHubAppGateway
{
    Task<ServiceResult<GitHubAppInfo>> VerifyAsync(long appId, string privateKeyPem, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GitHubInstallationInfo>>> ListInstallationsAsync(GitHubApp app, bool useCache, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>> ListRepositoriesAsync(GitHubApp app, long installationId, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(GitHubApp app, long installationId, string fullName, CancellationToken cancellationToken = default);

    /// <summary>Yalnızca verilen depoyu okuyabilen, önbelleğe alınmayan anahtar (deployment için).</summary>
    Task<ServiceResult<GitHubInstallationToken>> CreateRepositoryTokenAsync(GitHubApp app, long installationId, string fullName, CancellationToken cancellationToken = default);

    /// <summary>Uygulamanın önbellekteki kurulum/depo listelerini geçersiz kılar.</summary>
    void Invalidate(Guid appRecordId);
}

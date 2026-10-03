using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Services;

/// <summary>GitHub REST API. <c>jwt</c> uygulama kimliği, <c>token</c> kurulum erişim anahtarıdır; ikisi de loglanmaz.</summary>
public interface IGitHubApiClient
{
    Task<ServiceResult<GitHubAppInfo>> GetAppAsync(string jwt, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GitHubInstallationInfo>>> ListInstallationsAsync(string jwt, CancellationToken cancellationToken = default);

    /// <summary><paramref name="repositoryName"/> verilirse anahtar yalnızca o depoya ve içerik okumaya sınırlanır.</summary>
    Task<ServiceResult<GitHubInstallationToken>> CreateInstallationTokenAsync(string jwt, long installationId, string? repositoryName, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GitHubRepositoryInfo>>> ListRepositoriesAsync(string token, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(string token, string fullName, CancellationToken cancellationToken = default);

    Task<ServiceResult<GitHubManifestConversion>> ConvertManifestAsync(string code, CancellationToken cancellationToken = default);
}

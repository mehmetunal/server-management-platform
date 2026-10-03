using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Interfaces.Deployments;

/// <summary>
/// Depoları bir sağlayıcı hesabı üzerinden tanıtan Git entegrasyonu (GitHub App, GitLab vb.). Eklentiler uygular;
/// çekirdek yalnızca bu arayüzü bilir. <c>sourceId</c> entegrasyonun kendi biçimindeki bağlantı kimliğidir
/// (GitHub için uygulama + kurulum).
/// </summary>
public interface IGitIntegration
{
    /// <summary>Entegrasyonu sağlayan eklentinin SystemName'i; projede saklanır, değiştirilmez.</summary>
    string SystemName { get; }

    string DisplayName { get; }

    GitProvider Provider { get; }

    Task<ServiceResult<IReadOnlyList<GitSourceDto>>> ListSourcesAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GitRepositoryDto>>> ListRepositoriesAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<ServiceResult<GitRepositoryDto>> GetRepositoryAsync(string sourceId, string repository, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(string sourceId, string repository, CancellationToken cancellationToken = default);

    /// <summary>Depoyu https üzerinden çekmek için kısa ömürlü erişim anahtarı.</summary>
    Task<ServiceResult<GitAccessToken>> CreateAccessTokenAsync(string sourceId, string repository, CancellationToken cancellationToken = default);
}

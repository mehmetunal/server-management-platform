using ServerManager.Application.Common;
using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Services;

public interface IGitHubAppService
{
    /// <summary>Kayıtlı uygulamalar ve GitHub'dan canlı alınan kurulumları.</summary>
    Task<IReadOnlyList<GitHubAppDto>> GetAppsAsync(CancellationToken cancellationToken = default);

    /// <summary>Manifest akışını başlatır; dönüşte doğrulanacak tek kullanımlık durum değeri bir saat saklanır.</summary>
    Task<ServiceResult<GitHubManifestStartDto>> StartManifestAsync(GitHubManifestRequestDto dto, string panelBaseUrl, CancellationToken cancellationToken = default);

    /// <summary>GitHub'ın döndürdüğü kodla uygulamayı kaydeder; başarıda kurulum adresini döner.</summary>
    Task<ServiceResult<string>> CompleteManifestAsync(string? code, string? state, CancellationToken cancellationToken = default);

    Task<ServiceResult<string>> AddManualAsync(GitHubManualAppDto dto, CancellationToken cancellationToken = default);

    /// <summary>Kaydı yumuşak siler; GitHub'daki uygulamaya dokunmaz. Kullanan proje varsa reddedilir.</summary>
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>GitHub'da kurulum eklenip değiştiğinde listelerin hemen yenilenmesi için.</summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}

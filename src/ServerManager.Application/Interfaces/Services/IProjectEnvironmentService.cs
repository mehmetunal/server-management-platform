using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>
/// Proje ortam değişkenleri. İçerik tek bir şifreli <c>.env</c> kaydıdır (<c>DeploymentProject.EncryptedEnvironment</c>);
/// her değişiklikte çözülür, satır bazında düzenlenir (yorumlar ve sıra korunur) ve yeniden şifrelenir. Değişiklikler
/// sunucuya bir sonraki deploy'da veya "Uygula / Yeniden başlat" ile yazılır. Her yazma işlemi audit'e anahtar adlarıyla
/// (değerler olmadan) kaydedilir. Yetki kontrolü çağıran katmandadır (okuma: DeploymentView, yazma: DeploymentManage,
/// değer görme / dışa aktarma: DeploymentSecrets).
/// </summary>
public interface IProjectEnvironmentService
{
    Task<ServiceResult<ProjectEnvironmentDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Kayıtlı anahtarlar (.env sırasıyla); değerler dönmez. Kayıt çözülemezse hata döner.</summary>
    Task<ServiceResult<IReadOnlyList<string>>> GetEnvironmentKeysAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Tek değişkenin değerini döner ve görüntülemeyi audit'e yazar.</summary>
    Task<ServiceResult<string>> RevealValueAsync(Guid projectId, string key, CancellationToken cancellationToken = default);

    Task<ServiceResult> SetVariableAsync(Guid projectId, EnvironmentVariableDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteVariableAsync(Guid projectId, string key, CancellationToken cancellationToken = default);

    /// <summary>Yapıştırılan / yüklenen <c>.env</c> içeriğini içe aktarır.</summary>
    Task<ServiceResult<EnvironmentChangeResultDto>> ImportAsync(Guid projectId, string? content, EnvironmentImportMode mode, CancellationToken cancellationToken = default);

    /// <summary>Kayıtlı içeriği <c>.env</c> metni olarak döner ve indirmeyi audit'e yazar.</summary>
    Task<ServiceResult<string>> ExportAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Programatik ekleme/güncelleme (ör. yönetilen servislerin bağlantı bilgisini projeye yazmak için).
    /// Anahtarlar <c>EnvironmentFile</c> kurallarına, değerler tek satır kuralına uymalıdır; geçersiz giriş varsa hiçbir şey yazılmaz.
    /// <paramref name="overwrite"/> false ise var olan anahtarlar değiştirilmez ve sonuçta <c>Skipped</c> içinde döner.
    /// Diğer anahtarlar, yorumlar ve sıra korunur; yeni anahtarlar sona eklenir. Değişiklik varsa
    /// <c>project.env_update</c> audit kaydı (anahtar adlarıyla) yazılır. Sunucuya bir sonraki deploy / yeniden başlatmada uygulanır.
    /// </summary>
    Task<ServiceResult<EnvironmentChangeResultDto>> UpsertEnvironmentVariablesAsync(
        Guid projectId,
        IReadOnlyDictionary<string, string> vars,
        bool overwrite,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen anahtarları siler (ör. servis bağı kaldırılırken bağla eklenen değişkenler). Olmayan anahtarlar yok sayılır;
    /// değişiklik varsa <c>project.env_update</c> audit kaydı yazılır.
    /// </summary>
    Task<ServiceResult<EnvironmentChangeResultDto>> RemoveEnvironmentVariablesAsync(
        Guid projectId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default);
}

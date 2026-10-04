using ServerManager.Application.Common;
using ServerManager.Application.Notifications;

namespace ServerManager.Application.Interfaces.Backups;

/// <summary>
/// Yedek dosyalarının saklandığı yer (yerel disk, S3 uyumlu depolama…). Çekirdekte veya eklentide tanımlanır;
/// ayar alanları bildirim kanallarıyla aynı form altyapısını kullanır.
/// </summary>
public interface IBackupStorageProvider
{
    string SystemName { get; }

    string DisplayName { get; }

    string Description { get; }

    IReadOnlyList<NotificationSettingField> Fields { get; }

    IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings);

    /// <summary>Yazma, okuma ve silme iznini küçük bir deneme nesnesiyle doğrular.</summary>
    Task<ServiceResult> TestAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uzunluğu bilinmeyen, geri sarılamayan akışı yükler. Başarısız olursa yarım nesne bırakılmaz;
    /// <paramref name="content"/> okunurken oluşan hata yeniden fırlatılır.
    /// </summary>
    Task<ServiceResult> UploadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, Stream content, CancellationToken cancellationToken = default);

    Task<ServiceResult<Stream>> OpenReadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default);

    /// <summary>Nesne zaten yoksa başarılı sayılır.</summary>
    Task<ServiceResult> DeleteAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default);
}

using ServerManager.Application.Common;
using ServerManager.Application.Notifications;

namespace ServerManager.Application.Interfaces.Notifications;

/// <summary>
/// Bildirim gönderen kanal türü (e-posta, Telegram, Discord vb.). Eklentiler uygular; çekirdek yalnızca bu arayüzü
/// bilir. Ayarlar alan anahtarı → değer sözlüğü olarak verilir ve çekirdekte şifreli saklanır.
/// </summary>
public interface INotificationChannelProvider
{
    /// <summary>Sağlayan eklentinin SystemName'i; kanal kaydında saklanır, değiştirilmez.</summary>
    string SystemName { get; }

    string DisplayName { get; }

    string Description { get; }

    IReadOnlyList<NotificationSettingField> Fields { get; }

    /// <summary>Zorunluluk ve uzunluk dışındaki biçim kontrolleri; hata alan anahtarına bağlanır.</summary>
    IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings);

    Task<ServiceResult> SendAsync(IReadOnlyDictionary<string, string> settings, NotificationMessage message, CancellationToken cancellationToken = default);
}

using ServerManager.Application.Common;
using ServerManager.Application.Notifications;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Notifications;

public interface INotificationDispatcher
{
    /// <summary>
    /// Bildirimi kanalın sağlayıcısıyla gönderir, gönderim kaydını ekler ve kanalın son durumunu günceller.
    /// Değişiklikleri kaydetmek çağırana aittir; hata fırlatmaz.
    /// </summary>
    Task<ServiceResult> SendAsync(NotificationChannel channel, NotificationMessage message, Guid? alertEventId, CancellationToken cancellationToken = default);
}

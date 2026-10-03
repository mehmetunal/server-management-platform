using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class NotificationChannel : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Kanalı gönderen eklentinin SystemName'i (ör. Notifications.Telegram); değiştirilmez.</summary>
    public string ProviderSystemName { get; set; } = string.Empty;

    /// <summary>Sağlayıcı ayarları (JSON), tamamı şifreli.</summary>
    public string EncryptedSettings { get; set; } = string.Empty;

    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Warning;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastSentAt { get; set; }

    public string? LastError { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}

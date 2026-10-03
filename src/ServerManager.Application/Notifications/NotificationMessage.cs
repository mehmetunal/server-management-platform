using ServerManager.Domain.Enums;

namespace ServerManager.Application.Notifications;

/// <summary>Kanaldan bağımsız bildirim içeriği; sağlayıcı kendi biçimine (e-posta, Telegram, Discord) çevirir.</summary>
public sealed record NotificationMessage(
    NotificationKind Kind,
    AlertSeverity Severity,
    string Title,
    string Body,
    DateTime OccurredAt,
    string? ServerName = null,
    string? Url = null)
{
    public string SeverityText => Kind switch
    {
        NotificationKind.Recovery => "Düzeldi",
        NotificationKind.Test => "Test",
        _ => Severity == AlertSeverity.Critical ? "Kritik" : "Uyarı"
    };
}

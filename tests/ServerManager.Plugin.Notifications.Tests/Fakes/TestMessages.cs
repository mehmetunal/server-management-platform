using ServerManager.Application.Notifications;
using ServerManager.Domain.Enums;

namespace ServerManager.Plugin.Notifications.Tests.Fakes;

public static class TestMessages
{
    public static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    public static NotificationMessage Create(
        NotificationKind kind = NotificationKind.Firing,
        AlertSeverity severity = AlertSeverity.Critical,
        string title = "[Kritik] web-01: Yüksek CPU",
        string body = "CPU %95",
        string? url = "https://panel.example.com/Alerts") =>
        new(kind, severity, title, body, Now, "web-01", url);
}

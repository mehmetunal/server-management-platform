using ServerManager.Domain.Entities;

namespace ServerManager.Application.Alerting;

public sealed class AlertReconcileResult
{
    public List<AlertEvent> Opened { get; } = [];

    /// <summary>Koşulu düzelen alarmlar; kural izin veriyorsa düzelme bildirimi gönderilir.</summary>
    public List<AlertEvent> Recovered { get; } = [];

    /// <summary>Hedefi artık izlenmeyen (silinen sunucu, kapatılan kontrol) alarmlar; bildirim gönderilmez.</summary>
    public List<AlertEvent> Closed { get; } = [];

    public List<AlertEvent> Reminders { get; } = [];

    /// <summary>Daha önce açılmış ama açılış bildirimi başarıyla gönderilememiş (veya kaydedilememiş) alarmlar.</summary>
    public List<AlertEvent> PendingNotifications { get; } = [];
}

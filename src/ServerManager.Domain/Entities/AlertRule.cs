using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class AlertRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public AlertRuleKind Kind { get; set; }

    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    /// <summary>Yüzde (CPU/RAM/disk) veya gün (SSL). Diğer türlerde kullanılmaz.</summary>
    public double Threshold { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary>Boşsa kural tüm sunuculara uygulanır.</summary>
    public Guid? ServerId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool NotifyRecovery { get; set; } = true;

    /// <summary>Alarm sürerken bildirimin tekrarlanma aralığı; 0 tekrarlamaz.</summary>
    public int RepeatIntervalMinutes { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Server? Server { get; set; }

    public ICollection<AlertRuleChannel> Channels { get; set; } = new List<AlertRuleChannel>();
}

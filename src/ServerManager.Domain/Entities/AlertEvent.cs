using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class AlertEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RuleId { get; set; }

    public string RuleName { get; set; } = string.Empty;

    public AlertRuleKind Kind { get; set; }

    public AlertSeverity Severity { get; set; }

    public Guid? ServerId { get; set; }

    public string? ServerName { get; set; }

    /// <summary>Alarmın bağlı olduğu hedef (sunucu, uptime kontrolü, sertifika veya proje kimliği).</summary>
    public string TargetKey { get; set; } = string.Empty;

    public string TargetName { get; set; } = string.Empty;

    public AlertEventStatus Status { get; set; } = AlertEventStatus.Firing;

    public string Message { get; set; } = string.Empty;

    public double? Value { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolvedMessage { get; set; }

    /// <summary>Boşsa açılış bildirimi henüz başarıyla gönderilmemiştir; sonraki değerlendirmede yeniden denenir.</summary>
    public DateTime? LastNotifiedAt { get; set; }

    /// <summary>Son bildirimdeki değer; SSL'de kalan gün eşiklerinin bir kez bildirilmesi için tutulur.</summary>
    public double? NotifiedValue { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public string? AcknowledgedBy { get; set; }

    /// <summary>Art arda "düzeldi" sonuç sayısı; alarm, dalgalanmayı önlemek için birden fazla düzelme görünce kapanır.</summary>
    public int ConsecutiveOkCount { get; set; }
}

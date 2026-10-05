using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

/// <summary>Servis üzerinde yapılan kurulum, yeniden oluşturma, sürüm yükseltme veya kaldırma işlemi ve logu.</summary>
public class ManagedServiceOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServiceId { get; set; }

    public Guid ServerId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public ManagedServiceOperationKind Kind { get; set; }

    public ManagedServiceOperationStatus Status { get; set; } = ManagedServiceOperationStatus.Running;

    /// <summary>Ulaşılan son aşama (adım göstergesi için).</summary>
    public string? Stage { get; set; }

    public string? FromTag { get; set; }

    public string? ToTag { get; set; }

    /// <summary>Kaldırmada verinin (volume / klasör) de silinmesi istendi mi.</summary>
    public bool RemoveData { get; set; }

    public string? FailureReason { get; set; }

    public string Log { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FinishedAt { get; set; }
}

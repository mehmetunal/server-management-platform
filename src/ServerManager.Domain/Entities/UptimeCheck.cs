using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class UptimeCheck : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public UptimeCheckType Type { get; set; } = UptimeCheckType.Http;

    public string? Url { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    /// <summary>Kabul edilen HTTP durum kodları (ör. 200-399 veya 200,301).</summary>
    public string? AcceptedStatusCodes { get; set; }

    /// <summary>İsteğe bağlı; kontrol bu sunucunun alarmlarında ve sayfasında gösterilir.</summary>
    public Guid? ServerId { get; set; }

    public int IntervalSeconds { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 10;

    public bool IsEnabled { get; set; } = true;

    public UptimeStatus Status { get; set; } = UptimeStatus.Unknown;

    public DateTime? StatusChangedAt { get; set; }

    public DateTime? LastCheckedAt { get; set; }

    public int? LastResponseMs { get; set; }

    public int? LastStatusCode { get; set; }

    public string? LastError { get; set; }

    public int ConsecutiveFailures { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Server? Server { get; set; }
}

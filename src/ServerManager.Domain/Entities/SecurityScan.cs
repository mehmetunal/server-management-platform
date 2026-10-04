using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class SecurityScan
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public SecurityScanTrigger Trigger { get; set; }

    public SecurityScanStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>0-100; tamamlanmamış taramada boştur.</summary>
    public int? Score { get; set; }

    public int CriticalCount { get; set; }

    public int WarningCount { get; set; }

    /// <summary>Tarama root yetkisiyle çalıştı mı (değilse bazı kontroller "tespit edilemedi" olur).</summary>
    public bool IsPrivileged { get; set; }

    /// <summary>Bulgular, portlar, SSH ayarları vb. (JSON).</summary>
    public string? ReportJson { get; set; }

    public string? FailureReason { get; set; }

    public string? UserName { get; set; }
}

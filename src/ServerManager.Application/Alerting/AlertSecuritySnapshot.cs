namespace ServerManager.Application.Alerting;

/// <summary>Sunucunun son tamamlanan güvenlik taraması.</summary>
public sealed record AlertSecuritySnapshot(
    Guid ServerId,
    string ServerName,
    Guid ScanId,
    int? Score,
    int CriticalCount,
    int WarningCount,
    DateTime? CompletedAt);

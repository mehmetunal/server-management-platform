namespace ServerManager.Application.Monitoring;

/// <param name="History">Geçmiş/log hedefi başına silinen veya log metni temizlenen kayıt sayısı (süresiz saklananlar yer almaz).</param>
public sealed record MaintenanceResult(
    int AggregatedHours,
    int DeletedRawMetrics,
    int DeletedHourlyMetrics,
    int DeletedHealthChecks,
    IReadOnlyDictionary<RetentionTarget, int>? History = null);

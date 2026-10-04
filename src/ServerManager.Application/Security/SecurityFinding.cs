using ServerManager.Domain.Enums;

namespace ServerManager.Application.Security;

/// <param name="FixCommand">Yalnızca öneri olarak gösterilir; panel hiçbir düzeltmeyi kendiliğinden çalıştırmaz.</param>
public sealed record SecurityFinding(
    string Key,
    string Category,
    string Title,
    SecurityCheckStatus Status,
    string Detail,
    string? Recommendation = null,
    string? FixCommand = null);

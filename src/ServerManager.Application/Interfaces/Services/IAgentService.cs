using ServerManager.Application.Agent;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Agent;

namespace ServerManager.Application.Interfaces.Services;

public interface IAgentService
{
    Task<ServiceResult<AgentStatusDto>> GetStatusAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>Yeni token üretir; varsa eskisi geçersiz olur. Token yalnızca bu yanıtta döner.</summary>
    Task<ServiceResult<AgentTokenDto>> CreateTokenAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult> RevokeTokenAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<AgentReportOutcome> ReportAsync(string? token, string? agentVersion, string output, CancellationToken cancellationToken = default);

    string BuildInstallScript();

    /// <summary>SSH ile toplanamayan ve agent'ı sessiz kalan sunucuları çevrimdışı işaretler.</summary>
    Task<int> MarkSilentAgentsOfflineAsync(CancellationToken cancellationToken = default);
}

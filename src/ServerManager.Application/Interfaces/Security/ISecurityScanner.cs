using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Security;

namespace ServerManager.Application.Interfaces.Security;

public interface ISecurityScanner
{
    /// <summary>Sunucudan salt okunur komutlarla güvenlik bilgilerini toplar. Sunucuda hiçbir ayar değiştirilmez.</summary>
    Task<ServiceResult<SecurityFacts>> CollectAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);
}

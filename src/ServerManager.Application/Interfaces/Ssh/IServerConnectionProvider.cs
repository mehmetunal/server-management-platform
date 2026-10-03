using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Ssh;

public interface IServerConnectionProvider
{
    /// <summary>Kayıtlı ve host key'i doğrulanmış sunucu için çözülmüş kimlik bilgileriyle bağlantı bilgisini hazırlar.</summary>
    Task<ServiceResult<ServerConnection>> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
}

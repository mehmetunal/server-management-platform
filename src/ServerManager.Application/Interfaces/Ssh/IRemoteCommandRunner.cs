using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Ssh;

public interface IRemoteCommandRunner
{
    /// <summary>
    /// Tek SSH bağlantısı açar ve <paramref name="work"/> içinde komutların sırayla çalıştırılmasını sağlar.
    /// Bağlantı kurulamazsa (kimlik doğrulama, host key uyuşmazlığı, ağ) iş çalıştırılmadan hata döner.
    /// </summary>
    Task<ServiceResult<T>> RunAsync<T>(
        RemoteExecutionContext context,
        Func<IRemoteCommandExecutor, CancellationToken, Task<ServiceResult<T>>> work,
        CancellationToken cancellationToken = default);
}

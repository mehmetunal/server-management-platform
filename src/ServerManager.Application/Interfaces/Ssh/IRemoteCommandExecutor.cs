using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Ssh;

public interface IRemoteCommandExecutor
{
    Task<RemoteCommandOutput> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Komutu çalıştırır ve stdout/stderr çıktısını geldiği anda <paramref name="onOutput"/>'a sırayla iletir.
    /// Dönen sonuçta yalnızca çıktının son kısmı bulunur (hata mesajı çevirisi için).
    /// </summary>
    Task<RemoteCommandOutput> ExecuteStreamingAsync(
        RemoteCommand command,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken = default);
}

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

    /// <summary>
    /// stdout'u ikili akış olarak <paramref name="consumeOutput"/>'a verir (sonuna kadar okunmalıdır).
    /// Dönen sonuçta stdout boştur; stderr'in son kısmı bulunur.
    /// </summary>
    Task<RemoteCommandOutput> ExecuteWithOutputStreamAsync(
        RemoteCommand command,
        Func<Stream, CancellationToken, Task> consumeOutput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// stdin'e <see cref="RemoteCommand.StandardInput"/>'tan sonra <paramref name="produceInput"/>'un yazdığı ikili veriyi verir;
    /// yazma bitince stdin kapatılır. <paramref name="produceInput"/>'un fırlattığı hata, komut bittikten sonra yeniden fırlatılır.
    /// </summary>
    Task<RemoteCommandOutput> ExecuteWithInputStreamAsync(
        RemoteCommand command,
        Func<Stream, CancellationToken, Task> produceInput,
        CancellationToken cancellationToken = default);
}

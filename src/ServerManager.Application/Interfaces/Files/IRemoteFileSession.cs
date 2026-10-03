using ServerManager.Application.Files;

namespace ServerManager.Application.Interfaces.Files;

/// <summary>Tek bir SFTP bağlantısı üzerinde dosya işlemleri. Hatalar <see cref="RemoteFileException"/> olarak fırlatılır.</summary>
public interface IRemoteFileSession
{
    string HomeDirectory { get; }

    /// <summary>Sembolik bağlantıyı izlemeden bilgi döner; yol yoksa null.</summary>
    Task<RemoteFileInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemoteFileInfo>> ListAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>En fazla <paramref name="maxBytes"/> + 1 bayt okur; böylece çağıran sınırın aşıldığını anlayabilir.</summary>
    Task<byte[]> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken = default);

    Task WriteAsync(string path, byte[] content, bool createNew, CancellationToken cancellationToken = default);

    Task UploadAsync(Stream content, string path, CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task RenameAsync(string source, string destination, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Yalnızca boş klasörü siler.</summary>
    Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default);
}

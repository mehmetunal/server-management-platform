using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Files;

namespace ServerManager.Application.Interfaces.Files;

public interface IRemoteFileSystem
{
    /// <summary>SFTP bağlantısı açar ve işi çalıştırır. Bağlantı hataları iş çalıştırılmadan sonuç olarak döner.</summary>
    Task<ServiceResult<T>> RunAsync<T>(
        RemoteExecutionContext context,
        Func<IRemoteFileSession, CancellationToken, Task<ServiceResult<T>>> work,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RemoteFileStream>> OpenReadAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default);

    Task<ServiceResult> CopyAsync(RemoteExecutionContext context, string source, string destination, CancellationToken cancellationToken = default);

    /// <summary>Yoldaki sembolik bağlantıları sunucuda çözer (readlink -f). Sunucuda sudo açıksa sudo ile çalışır.</summary>
    Task<ServiceResult<RemotePathResolution>> ResolveAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteRecursiveAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default);

    /// <summary>Sunucuda sudo açıksa sudo ile çalışır.</summary>
    Task<ServiceResult> ChangeModeAsync(RemoteExecutionContext context, string path, string mode, bool recursive, CancellationToken cancellationToken = default);

    /// <summary>Sunucuda sudo açıksa sudo ile çalışır.</summary>
    Task<ServiceResult> ChangeOwnerAsync(RemoteExecutionContext context, string path, string? owner, string? group, bool recursive, CancellationToken cancellationToken = default);
}

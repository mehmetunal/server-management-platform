using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Application.Interfaces.Services;

public interface IFileService
{
    /// <param name="path">Boşsa SSH kullanıcısının ev klasörü listelenir.</param>
    Task<ServiceResult<FileListingDto>> ListAsync(Guid serverId, string? path, CancellationToken cancellationToken = default);

    Task<ServiceResult<FileContentDto>> ReadAsync(Guid serverId, string path, CancellationToken cancellationToken = default);

    Task<ServiceResult<FileSaveResultDto>> SaveAsync(Guid serverId, SaveFileDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> CreateAsync(Guid serverId, CreateFileEntryDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> MoveAsync(Guid serverId, MoveFileDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> CopyAsync(Guid serverId, CopyFileDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid serverId, DeleteFileDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> ChangePermissionsAsync(Guid serverId, ChangePermissionsDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UploadAsync(Guid serverId, UploadFileDto dto, Stream content, long length, CancellationToken cancellationToken = default);

    Task<ServiceResult<RemoteFileStream>> DownloadAsync(Guid serverId, string path, CancellationToken cancellationToken = default);
}

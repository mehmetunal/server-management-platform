using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Web.Models;

public sealed class BackupStorageIndexViewModel
{
    public IReadOnlyList<BackupStorageListItemDto> Storages { get; init; } = [];

    public IReadOnlyList<BackupStorageProviderDto> Providers { get; init; } = [];
}

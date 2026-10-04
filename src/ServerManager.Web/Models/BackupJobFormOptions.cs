using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class BackupJobFormOptions
{
    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];

    public IReadOnlyList<BackupStorageOptionDto> Storages { get; init; } = [];

    public string TimeZone { get; init; } = string.Empty;
}

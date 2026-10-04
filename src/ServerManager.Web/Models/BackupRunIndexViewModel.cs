using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class BackupRunIndexViewModel
{
    public required BackupRunListViewModel List { get; init; }

    public IReadOnlyList<BackupJobListItemDto> Jobs { get; init; } = [];

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];
}

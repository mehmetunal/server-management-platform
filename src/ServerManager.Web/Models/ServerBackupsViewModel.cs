using ServerManager.Application.DTOs.Backups;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerBackupsViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required IReadOnlyList<BackupJobListItemDto> Jobs { get; init; }

    public required BackupRunListViewModel Runs { get; init; }
}

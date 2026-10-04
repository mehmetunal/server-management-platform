using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Web.Models;

public sealed class BackupJobDetailsViewModel
{
    public required BackupJobDetailsDto Details { get; init; }

    public IReadOnlyList<BackupRunListItemDto> RecentRuns { get; init; } = [];
}

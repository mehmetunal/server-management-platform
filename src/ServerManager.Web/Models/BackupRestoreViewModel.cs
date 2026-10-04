using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class BackupRestoreViewModel
{
    public required BackupRestoreFormDto Form { get; init; }

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];
}

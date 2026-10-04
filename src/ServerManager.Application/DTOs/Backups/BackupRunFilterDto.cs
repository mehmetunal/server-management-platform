using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupRunFilterDto
{
    public string? Search { get; set; }

    public Guid? JobId { get; set; }

    public Guid? ServerId { get; set; }

    public BackupOperation? Operation { get; set; }

    public BackupRunStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

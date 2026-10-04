using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Web.Models;

public sealed class BackupRunListViewModel
{
    public required PagedResult<BackupRunListItemDto> Runs { get; init; }

    public required BackupRunFilterDto Filter { get; init; }

    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(Filter.Search) || Filter.Status.HasValue || Filter.Operation.HasValue
        || Filter.JobId.HasValue || Filter.ServerId.HasValue;

    public PagerModel ToPager() => new()
    {
        Page = Runs.Page,
        TotalPages = Runs.TotalPages,
        TotalCount = Runs.TotalCount,
        Controller = "BackupRuns",
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["jobId"] = Filter.JobId?.ToString(),
            ["serverId"] = Filter.ServerId?.ToString(),
            ["operation"] = Filter.Operation.HasValue ? ((int)Filter.Operation.Value).ToString() : null,
            ["status"] = Filter.Status.HasValue ? ((int)Filter.Status.Value).ToString() : null,
            ["pageSize"] = Filter.PageSize == Paging.DefaultPageSize ? null : Filter.PageSize.ToString()
        }
    };
}

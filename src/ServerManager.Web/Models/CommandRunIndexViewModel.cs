using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Commands;

namespace ServerManager.Web.Models;

public sealed class CommandRunIndexViewModel
{
    public required PagedResult<CommandRunListItemDto> Runs { get; init; }

    public required CommandRunFilterDto Filter { get; init; }

    public bool HasFilter => !string.IsNullOrWhiteSpace(Filter.Search);

    public bool HasRunning => Runs.Items.Any(r => r.Status == Domain.Enums.CommandRunStatus.Running);

    public PagerModel ToPager() => new()
    {
        Page = Runs.Page,
        TotalPages = Runs.TotalPages,
        TotalCount = Runs.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["pageSize"] = Filter.PageSize == Paging.DefaultPageSize ? null : Filter.PageSize.ToString()
        }
    };
}

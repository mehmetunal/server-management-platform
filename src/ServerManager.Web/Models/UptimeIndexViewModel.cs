using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.DTOs.Uptime;

namespace ServerManager.Web.Models;

public sealed class UptimeIndexViewModel
{
    public required PagedResult<UptimeCheckListItemDto> Checks { get; init; }

    public required UptimeFilterDto Filter { get; init; }

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];

    public PagerModel ToPager() => new()
    {
        Page = Checks.Page,
        TotalPages = Checks.TotalPages,
        TotalCount = Checks.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["status"] = Filter.Status is { } status ? ((int)status).ToString() : null,
            ["serverId"] = Filter.ServerId?.ToString()
        }
    };
}

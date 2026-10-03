using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerIndexViewModel
{
    public required PagedResult<ServerListItemDto> Servers { get; init; }

    public required ServerFilterDto Filter { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public PagerModel ToPager() => new()
    {
        Page = Servers.Page,
        TotalPages = Servers.TotalPages,
        TotalCount = Servers.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["status"] = Filter.Status.HasValue ? ((int)Filter.Status.Value).ToString() : null,
            ["environment"] = Filter.Environment.HasValue ? ((int)Filter.Environment.Value).ToString() : null,
            ["tag"] = Filter.Tag
        }
    };
}

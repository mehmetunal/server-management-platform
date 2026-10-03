using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssl;

namespace ServerManager.Web.Models;

public sealed class SslIndexViewModel
{
    public required PagedResult<SslMonitorListItemDto> Monitors { get; init; }

    public required SslMonitorFilterDto Filter { get; init; }

    public PagerModel ToPager() => new()
    {
        Page = Monitors.Page,
        TotalPages = Monitors.TotalPages,
        TotalCount = Monitors.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["status"] = Filter.Status is { } status ? ((int)status).ToString() : null
        }
    };
}

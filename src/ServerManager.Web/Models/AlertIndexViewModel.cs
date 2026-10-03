using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class AlertIndexViewModel
{
    public required PagedResult<AlertEventDto> Events { get; init; }

    public required AlertEventFilterDto Filter { get; init; }

    public AlertSummaryDto? Summary { get; init; }

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];

    public PagerModel ToPager() => new()
    {
        Page = Events.Page,
        TotalPages = Events.TotalPages,
        TotalCount = Events.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["status"] = Filter.Status is { } status ? ((int)status).ToString() : null,
            ["severity"] = Filter.Severity is { } severity ? ((int)severity).ToString() : null,
            ["serverId"] = Filter.ServerId?.ToString()
        }
    };
}

using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class ProjectIndexViewModel
{
    public required PagedResult<ProjectListItemDto> Projects { get; init; }

    public required ProjectFilterDto Filter { get; init; }

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];

    public PagerModel ToPager() => new()
    {
        Page = Projects.Page,
        TotalPages = Projects.TotalPages,
        TotalCount = Projects.TotalCount,
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["serverId"] = Filter.ServerId?.ToString()
        }
    };
}

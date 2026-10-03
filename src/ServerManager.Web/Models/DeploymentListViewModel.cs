using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Web.Models;

/// <summary>Deployment listesi; tek başına sayfada veya proje/sunucu sayfasına gömülü olarak kullanılır.</summary>
public sealed class DeploymentListViewModel
{
    public required PagedResult<DeploymentListItemDto> Deployments { get; init; }

    public required DeploymentFilterDto Filter { get; init; }

    public bool ShowProject { get; init; } = true;

    public bool ShowServer { get; init; } = true;

    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(Filter.Search) || Filter.Status.HasValue
        || (ShowProject && Filter.ProjectId.HasValue) || (ShowServer && Filter.ServerId.HasValue);

    public PagerModel ToPager() => new()
    {
        Page = Deployments.Page,
        TotalPages = Deployments.TotalPages,
        TotalCount = Deployments.TotalCount,
        Controller = "Deployments",
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = Filter.Search,
            ["projectId"] = Filter.ProjectId?.ToString(),
            ["serverId"] = Filter.ServerId?.ToString(),
            ["status"] = Filter.Status.HasValue ? ((int)Filter.Status.Value).ToString() : null,
            ["pageSize"] = Filter.PageSize == Paging.DefaultPageSize ? null : Filter.PageSize.ToString(),
            ["embedded"] = ShowProject && ShowServer ? null : (ShowProject ? "server" : "project")
        }
    };
}

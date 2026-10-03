using ServerManager.Application.Common;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class ProjectFilterDto
{
    public string? Search { get; set; }

    public Guid? ServerId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

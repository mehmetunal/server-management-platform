using ServerManager.Application.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

public sealed class DeploymentFilterDto
{
    public string? Search { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? ServerId { get; set; }

    public DeploymentStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

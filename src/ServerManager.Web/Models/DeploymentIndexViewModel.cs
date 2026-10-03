using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class DeploymentIndexViewModel
{
    public required DeploymentListViewModel List { get; init; }

    public IReadOnlyList<ProjectListItemDto> Projects { get; init; } = [];

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];
}

using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Web.Models;

public sealed class ProjectDetailsViewModel
{
    public required ProjectDetailsDto Project { get; init; }

    public IReadOnlyList<DomainListItemDto> Domains { get; init; } = [];

    public required DeploymentListViewModel Deployments { get; init; }
}

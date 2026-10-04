using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Models;

public sealed class DomainPanelViewModel
{
    public required Guid ProjectId { get; init; }

    public required DeploymentBuildType BuildType { get; init; }

    public required IReadOnlyList<DomainListItemDto> Domains { get; init; }

    public required bool CanManage { get; init; }
}

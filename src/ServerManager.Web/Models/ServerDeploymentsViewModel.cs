using ServerManager.Application.DTOs.Deployments;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerDeploymentsViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public IReadOnlyList<ProjectListItemDto> Projects { get; init; } = [];

    public required DeploymentListViewModel Deployments { get; init; }
}

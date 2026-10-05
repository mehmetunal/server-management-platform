using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Web.Models;

public sealed class EnvironmentPanelViewModel
{
    public required ProjectEnvironmentDto Environment { get; init; }

    public bool CanManage { get; init; }

    public bool CanReveal { get; init; }

    public bool CanApply { get; init; }

    public Guid? RunningDeploymentId { get; init; }
}

using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.ManagedServices;

namespace ServerManager.Web.Models;

public sealed class ProjectDetailsViewModel
{
    public required ProjectDetailsDto Project { get; init; }

    public IReadOnlyList<DomainListItemDto> Domains { get; init; } = [];

    public required DeploymentListViewModel Deployments { get; init; }

    public required EnvironmentPanelViewModel Environment { get; init; }

    public required WebhookPanelViewModel Webhook { get; init; }

    /// <summary>Projeye bağlı yönetilen servisler ("Projeye bağla").</summary>
    public IReadOnlyList<ProjectServiceLinkDto> ServiceLinks { get; init; } = [];

    /// <summary>Bağı kaldırma: services.manage + deployment.manage.</summary>
    public bool CanManageServiceLinks { get; init; }
}

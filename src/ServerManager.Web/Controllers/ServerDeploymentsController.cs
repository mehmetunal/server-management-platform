using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Servers;
using ServerManager.Web.Models;

namespace ServerManager.Web.Controllers;

/// <summary>Sunucu sayfasındaki Deployments sekmesi: o sunucuya dağıtılan projeler ve son deployment'lar.</summary>
[HasPermission(Permissions.DeploymentView)]
public class ServerDeploymentsController : Controller
{
    private readonly IProjectService _projectService;
    private readonly IDeploymentService _deploymentService;
    private readonly ServerPageBuilder _pageBuilder;

    public ServerDeploymentsController(IProjectService projectService, IDeploymentService deploymentService, ServerPageBuilder pageBuilder)
    {
        _projectService = projectService;
        _deploymentService = deploymentService;
        _pageBuilder = pageBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid id, CancellationToken cancellationToken)
    {
        var page = await _pageBuilder.BuildAsync(id, ServerPageViewModel.DeploymentsTab, MetricRange.OneHour, cancellationToken);
        if (page is null)
            return NotFound();

        var filter = new DeploymentFilterDto { ServerId = id, PageSize = 10 };
        return View(new ServerDeploymentsViewModel
        {
            Page = page,
            Projects = await _projectService.GetByServerAsync(id, cancellationToken),
            Deployments = new DeploymentListViewModel
            {
                Deployments = await _deploymentService.SearchAsync(filter, cancellationToken),
                Filter = filter,
                ShowServer = false
            }
        });
    }
}

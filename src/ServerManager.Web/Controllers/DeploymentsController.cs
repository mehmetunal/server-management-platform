using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Deployments;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.DeploymentView)]
public partial class DeploymentsController : Controller
{
    private const int FilterProjectLimit = 100;

    private readonly IDeploymentService _deploymentService;
    private readonly IProjectService _projectService;
    private readonly DeploymentManager _deploymentManager;
    private readonly ICurrentUserService _currentUser;

    public DeploymentsController(
        IDeploymentService deploymentService,
        IProjectService projectService,
        DeploymentManager deploymentManager,
        ICurrentUserService currentUser)
    {
        _deploymentService = deploymentService;
        _projectService = projectService;
        _deploymentManager = deploymentManager;
        _currentUser = currentUser;
    }

    /// <param name="embedded">"project" veya "server": liste proje/sunucu sayfasına gömülü; ilgili sütun gizlenir.</param>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DeploymentFilterDto filter, string? embedded, CancellationToken cancellationToken)
    {
        var deployments = await _deploymentService.SearchAsync(filter, cancellationToken);
        var list = new DeploymentListViewModel
        {
            Deployments = deployments,
            Filter = filter,
            ShowProject = embedded != "project",
            ShowServer = embedded != "server"
        };
        if (Request.IsAjax())
            return PartialView("_DeploymentList", list);

        var projects = await _projectService.SearchAsync(new ProjectFilterDto { PageSize = FilterProjectLimit }, cancellationToken);
        return View(new DeploymentIndexViewModel
        {
            List = list,
            Projects = projects.Items,
            Servers = await _projectService.GetServerOptionsAsync(cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deploymentService.GetAsync(id, includeLog: false, cancellationToken);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentExecute)]
    public IActionResult Cancel(Guid id)
    {
        var actor = new DeploymentActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var result = _deploymentManager.Cancel(id, actor);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Deployment iptal edilemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentExecute)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> Redeploy(Guid id, CancellationToken cancellationToken)
    {
        var actor = new DeploymentActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var result = await _deploymentManager.RedeployAsync(id, actor, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yeniden deploy başlatılamadı.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id = result.Data }));
    }

    [HttpGet]
    public async Task<IActionResult> Log(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deploymentService.GetAsync(id, includeLog: true, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        var deployment = result.Data!;
        var text = AnsiEscape().Replace(deployment.Log, string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var fileName = $"deployment-{deployment.ProjectName}-{deployment.StartedAt:yyyyMMdd-HHmmss}.log";
        return File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", SafeFileName(fileName));
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-'));

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();
}

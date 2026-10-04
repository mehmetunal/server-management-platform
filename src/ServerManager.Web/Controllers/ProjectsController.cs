using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Domain.Enums;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Deployments;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

[HasPermission(Permissions.DeploymentView)]
public class ProjectsController : Controller
{
    public const string ServerOptionsKey = "ServerOptions";
    public const string GitIntegrationsKey = "GitIntegrations";

    private readonly IProjectService _projectService;
    private readonly IDeploymentDomainService _domains;
    private readonly IDeploymentService _deploymentService;
    private readonly DeploymentManager _deploymentManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IGitIntegrationRegistry _gitIntegrations;

    public ProjectsController(
        IProjectService projectService,
        IDeploymentDomainService domains,
        IDeploymentService deploymentService,
        DeploymentManager deploymentManager,
        ICurrentUserService currentUser,
        IGitIntegrationRegistry gitIntegrations)
    {
        _gitIntegrations = gitIntegrations;
        _projectService = projectService;
        _domains = domains;
        _deploymentService = deploymentService;
        _deploymentManager = deploymentManager;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ProjectFilterDto filter, CancellationToken cancellationToken)
    {
        var projects = await _projectService.SearchAsync(filter, cancellationToken);
        var model = new ProjectIndexViewModel
        {
            Projects = projects,
            Filter = filter,
            Servers = Request.IsAjax() ? [] : await _projectService.GetServerOptionsAsync(cancellationToken)
        };
        return Request.IsAjax() ? PartialView("_ProjectList", model) : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetDetailsAsync(id, cancellationToken);
        if (!project.IsSuccess)
            return NotFound();

        var filter = new DeploymentFilterDto { ProjectId = id, PageSize = 10 };
        var deployments = await _deploymentService.SearchAsync(filter, cancellationToken);
        return View(new ProjectDetailsViewModel
        {
            Project = project.Data!,
            Domains = await _domains.ListAsync(id, cancellationToken),
            Deployments = new DeploymentListViewModel { Deployments = deployments, Filter = filter, ShowProject = false }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Domains(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetDetailsAsync(id, cancellationToken);
        if (!project.IsSuccess)
            return NotFound();

        return PartialView("_DomainTable", DomainPanel(id, project.Data!.BuildType, await _domains.ListAsync(id, cancellationToken)));
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> ProxyStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _domains.GetProxyStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Vekil durumu okunamadı.");

        return Ok(ApiResponse<ProxyStatusDto>.Success(result.Data, result.Data!.Message));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> InstallProxy(Guid id, CancellationToken cancellationToken)
    {
        var result = await _domains.InstallProxyAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Vekil kurulamadı.");
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> ApplyRouting(Guid id, CancellationToken cancellationToken)
    {
        var result = await _domains.ApplyAsync(id, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Yönlendirme uygulanamadı.");
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> SaveDomain(Guid id, DomainFormDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _domains.SaveAsync(id, dto, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Domain kaydedilemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> DeleteDomain(Guid id, Guid domainId, string? confirmationHost, CancellationToken cancellationToken)
    {
        var result = await _domains.DeleteAsync(id, domainId, confirmationHost, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Domain silinemedi.");
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Create(Guid? serverId, CancellationToken cancellationToken)
    {
        SetFormData();
        ViewData[ServerOptionsKey] = await _projectService.GetServerOptionsAsync(cancellationToken);
        return View(new CreateProjectDto { ServerId = serverId ?? Guid.Empty });
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Create(CreateProjectDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _projectService.CreateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Proje eklenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id = result.Data }));
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetForEditAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        SetFormData();
        ViewData[ServerOptionsKey] = await _projectService.GetServerOptionsAsync(cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Edit(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        dto.Id = id;
        var result = await _projectService.UpdateAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Proje güncellenemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Details), new { id }));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Delete(Guid id, string? confirmationName, bool hardDelete, CancellationToken cancellationToken)
    {
        var result = await _projectService.DeleteAsync(id, confirmationName, hardDelete, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Proje silinemedi.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(Index)));
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentExecute)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> Branches(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.ListBranchesAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Dallar listelenemedi.");

        return Ok(ApiResponse<GitBranchListDto>.Success(result.Data, $"{result.Data!.Branches.Count} dal bulundu."));
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentLookup)]
    public async Task<IActionResult> GitSources(CancellationToken cancellationToken)
    {
        var result = await _projectService.GetGitSourcesAsync(cancellationToken);
        return Ok(ApiResponse<GitSourceListDto>.Success(result, $"{result.Sources.Count} bağlantı bulundu."));
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentLookup)]
    public async Task<IActionResult> GitRepositories(string? source, CancellationToken cancellationToken)
    {
        var result = await _projectService.ListGitRepositoriesAsync(source, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Depolar listelenemedi.");

        return Ok(ApiResponse<IReadOnlyList<GitRepositoryDto>>.Success(result.Data, $"{result.Data!.Count} depo bulundu."));
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentLookup)]
    public async Task<IActionResult> GitBranches(string? source, string? repository, CancellationToken cancellationToken)
    {
        var result = await _projectService.ListGitBranchesAsync(source, repository, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Dallar listelenemedi.");

        return Ok(ApiResponse<IReadOnlyList<string>>.Success(result.Data, $"{result.Data!.Count} dal bulundu."));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentLookup)]
    public async Task<IActionResult> RemoteBranches(RemoteBranchQueryDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _projectService.ListRemoteBranchesAsync(dto, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Dallar listelenemedi.");

        return Ok(ApiResponse<IReadOnlyList<string>>.Success(result.Data, $"{result.Data!.Count} dal bulundu."));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentExecute)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> Deploy(Guid id, StartDeploymentDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var actor = new DeploymentActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var result = await _deploymentManager.StartAsync(id, dto, actor, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Deployment başlatılamadı.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(DeploymentsController.Details), "Deployments", new { id = result.Data }));
    }

    private DomainPanelViewModel DomainPanel(Guid projectId, DeploymentBuildType buildType, IReadOnlyList<DomainListItemDto> domains) =>
        new()
        {
            ProjectId = projectId,
            BuildType = buildType,
            Domains = domains,
            CanManage = User.HasPermission(Permissions.DeploymentManage)
        };

    private void SetFormData() =>
        ViewData[GitIntegrationsKey] = _gitIntegrations.GetEnabled().Select(i => i.DisplayName).ToList();
}

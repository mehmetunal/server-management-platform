using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Deployments;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Api.V1;

[Route("api/v1/projects")]
[HasPermission(Permissions.DeploymentView)]
public sealed class ProjectsApiController : ApiV1ControllerBase
{
    private readonly IProjectService _projects;
    private readonly IDeploymentService _deployments;
    private readonly DeploymentManager _deploymentManager;
    private readonly ICurrentUserService _currentUser;

    public ProjectsApiController(IProjectService projects, IDeploymentService deployments, DeploymentManager deploymentManager, ICurrentUserService currentUser)
    {
        _projects = projects;
        _deployments = deployments;
        _deploymentManager = deploymentManager;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiPage<ApiProject>>> List(string? search, Guid? serverId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _projects.SearchAsync(
            new ProjectFilterDto { Search = search, ServerId = serverId, Page = NormalizePage(page), PageSize = NormalizePageSize(pageSize) },
            cancellationToken);
        return ApiPage<ApiProject>.From(result, ApiProject.From);
    }

    /// <summary>Projenin deployment geçmişi (yeniden eskiye).</summary>
    [HttpGet("{id:guid}/deployments")]
    public async Task<ActionResult<ApiPage<ApiDeployment>>> Deployments(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetDetailsAsync(id, cancellationToken);
        if (!project.IsSuccess)
            return Problem(project, "Proje bulunamadı");

        var result = await _deployments.SearchAsync(
            new DeploymentFilterDto { ProjectId = id, Page = NormalizePage(page), PageSize = NormalizePageSize(pageSize) },
            cancellationToken);
        return ApiPage<ApiDeployment>.From(result, ApiDeployment.From);
    }

    /// <summary>Deploy başlatır (202). Durumu <c>GET /api/v1/deployments/{id}</c> ile izleyin.</summary>
    [HttpPost("{id:guid}/deployments")]
    [HasPermission(Permissions.DeploymentExecute)]
    [ProducesResponseType<ApiAccepted>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Deploy(Guid id, [FromBody] ApiStartDeployment? request, CancellationToken cancellationToken)
    {
        var result = await _deploymentManager.StartAsync(id, new StartDeploymentDto { CommitSha = request?.CommitSha }, Actor, cancellationToken);
        if (!result.IsSuccess)
            return Problem(result, "Deployment başlatılamadı");

        return Accepted(StatusUrl(result.Data), new ApiAccepted(result.Data, result.Message ?? "Deployment başlatıldı.", StatusUrl(result.Data)));
    }

    /// <summary>Build etmeden yeniden başlatır (ortam değişkenlerini uygular). Yeni bir deployment kaydı (Kind = Restart) açar.</summary>
    [HttpPost("{id:guid}/restart")]
    [HasPermission(Permissions.DeploymentExecute)]
    [ProducesResponseType<ApiAccepted>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Restart(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deploymentManager.RestartAsync(id, Actor, cancellationToken);
        if (!result.IsSuccess)
            return Problem(result, "Yeniden başlatma başlatılamadı");

        return Accepted(StatusUrl(result.Data), new ApiAccepted(result.Data, result.Message ?? "Yeniden başlatma başlatıldı.", StatusUrl(result.Data)));
    }

    private DeploymentActor Actor => new(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);

    private static string StatusUrl(Guid deploymentId) => $"/api/v1/deployments/{deploymentId}";
}

[Route("api/v1/deployments")]
[HasPermission(Permissions.DeploymentView)]
public sealed partial class DeploymentsApiController : ApiV1ControllerBase
{
    public const int MaxTail = 5000;

    private readonly IDeploymentService _deployments;

    public DeploymentsApiController(IDeploymentService deployments)
    {
        _deployments = deployments;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiDeployment>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deployments.GetAsync(id, includeLog: false, cancellationToken);
        return result.IsSuccess ? ApiDeployment.From(result.Data!) : Problem(result, "Deployment bulunamadı");
    }

    /// <summary>Deployment logunun son <paramref name="tail"/> satırı (ANSI renk kodları temizlenmiş; en çok 5000).</summary>
    [HttpGet("{id:guid}/log")]
    public async Task<ActionResult<ApiDeploymentLog>> Log(Guid id, int tail = 200, CancellationToken cancellationToken = default)
    {
        var result = await _deployments.GetAsync(id, includeLog: true, cancellationToken);
        if (!result.IsSuccess)
            return Problem(result, "Deployment bulunamadı");

        var deployment = result.Data!;
        var text = AnsiEscape().Replace(deployment.Log, string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Length == 0 ? [] : text.TrimEnd('\n').Split('\n');
        var take = Math.Clamp(tail, 1, MaxTail);
        var selected = lines.Length > take ? lines[^take..] : lines;
        return new ApiDeploymentLog(deployment.Id, deployment.Status.ToString(), deployment.IsRunning, lines.Length, lines.Length > take, selected);
    }

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Deployments;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>Projenin çalışan container'ları: çalışma logları (docker logs) ve build olmadan yeniden başlatma.</summary>
[HasPermission(Permissions.DeploymentView)]
public class ProjectRuntimeController : Controller
{
    private readonly IProjectRuntimeService _runtime;
    private readonly DeploymentManager _deploymentManager;
    private readonly ICurrentUserService _currentUser;

    public ProjectRuntimeController(IProjectRuntimeService runtime, DeploymentManager deploymentManager, ICurrentUserService currentUser)
    {
        _runtime = runtime;
        _deploymentManager = deploymentManager;
        _currentUser = currentUser;
    }

    [HttpGet]
    [HasPermission(Permissions.DockerView)]
    public async Task<IActionResult> Containers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _runtime.GetContainersAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Container'lar okunamadı.");

        return Ok(ApiResponse<IReadOnlyList<ProjectContainerDto>>.Success(result.Data, $"{result.Data!.Count} container bulundu."));
    }

    [HttpGet]
    [HasPermission(Permissions.DockerView)]
    public async Task<IActionResult> Logs(Guid id, ProjectLogQuery query, CancellationToken cancellationToken)
    {
        var result = await _runtime.GetLogsAsync(id, query, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Loglar alınamadı.");

        return Ok(ApiResponse<ProjectLogsDto>.Success(result.Data));
    }

    /// <summary>Ortam değişkenlerini uygular: .env ve compose override yeniden yazılır, container'lar build olmadan yeniden oluşturulur.</summary>
    [HttpPost]
    [HasPermission(Permissions.DeploymentExecute)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> Restart(Guid id, CancellationToken cancellationToken)
    {
        var actor = new DeploymentActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var result = await _deploymentManager.RestartAsync(id, actor, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Yeniden başlatma başlatılamadı.");

        return this.ApiSuccess(result.Message, Url.Action(nameof(DeploymentsController.Details), "Deployments", new { id = result.Data }));
    }
}

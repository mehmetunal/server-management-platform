using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>Proje sayfasındaki "Ortam değişkenleri" sekmesi. Değerler yalnızca tek tek (yetki ve audit ile) gösterilir.</summary>
[HasPermission(Permissions.DeploymentView)]
public class ProjectEnvironmentController : Controller
{
    private readonly IProjectEnvironmentService _environment;
    private readonly IProjectService _projects;

    public ProjectEnvironmentController(IProjectEnvironmentService environment, IProjectService projects)
    {
        _environment = environment;
        _projects = projects;
    }

    [HttpGet]
    public async Task<IActionResult> Panel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _environment.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        var project = await _projects.GetDetailsAsync(id, cancellationToken);
        return PartialView("~/Views/Projects/_EnvironmentTable.cshtml", Panel(result.Data!, project.Data?.RunningDeploymentId));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentSecrets)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentLookup)]
    public async Task<IActionResult> Reveal(Guid id, string? key, CancellationToken cancellationToken)
    {
        var result = await _environment.RevealValueAsync(id, key ?? string.Empty, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Değer gösterilemedi.");

        return Ok(ApiResponse<string>.Success(result.Data, string.Empty));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Save(Guid id, EnvironmentVariableDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return this.ApiInvalidModel();

        var result = await _environment.SetVariableAsync(id, dto, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Değişken kaydedilemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    public async Task<IActionResult> Delete(Guid id, string? key, CancellationToken cancellationToken)
    {
        var result = await _environment.DeleteVariableAsync(id, key ?? string.Empty, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "Değişken silinemedi.");
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> Import(Guid id, string? content, EnvironmentImportMode mode, CancellationToken cancellationToken)
    {
        var result = await _environment.ImportAsync(id, content, mode, cancellationToken);
        return result.IsSuccess ? this.ApiSuccess(result.Message) : this.ApiFailure(result, "İçe aktarma yapılamadı.");
    }

    [HttpGet]
    [HasPermission(Permissions.DeploymentSecrets)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentLookup)]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetDetailsAsync(id, cancellationToken);
        if (!project.IsSuccess)
            return NotFound();

        var result = await _environment.ExportAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, ".env indirilemedi.");

        Response.Headers.CacheControl = "no-store";
        return File(Encoding.UTF8.GetBytes(result.Data!), "text/plain; charset=utf-8", $"{project.Data!.Slug}.env");
    }

    private EnvironmentPanelViewModel Panel(ProjectEnvironmentDto environment, Guid? runningDeploymentId) => new()
    {
        Environment = environment,
        CanManage = User.HasPermission(Permissions.DeploymentManage),
        CanReveal = User.HasPermission(Permissions.DeploymentSecrets),
        CanApply = User.HasPermission(Permissions.DeploymentExecute),
        RunningDeploymentId = runningDeploymentId
    };
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.Framework.Mvc;
using ServerManager.Web.Models;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>Proje sayfasındaki "Push ile otomatik deploy" ayarları; webhook'un kendisi <see cref="ProjectWebhookApiController"/>.</summary>
[HasPermission(Permissions.DeploymentView)]
public class ProjectWebhookController : Controller
{
    private readonly IProjectWebhookService _webhooks;
    private readonly DeploymentOptions _options;

    public ProjectWebhookController(IProjectWebhookService webhooks, IOptions<DeploymentOptions> options)
    {
        _webhooks = webhooks;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Panel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _webhooks.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound();

        return PartialView("~/Views/Projects/_WebhookPanel.cshtml", WebhookPanelViewModel.Create(result.Data!, WebhookUrl(this, _options, id), User.HasPermission(Permissions.DeploymentManage)));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> Toggle(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var result = await _webhooks.SetAutoDeployAsync(id, enabled, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Ayar kaydedilemedi.");

        return Ok(ApiResponse<ProjectWebhookSecretDto>.Success(result.Data, result.Data!.Message));
    }

    [HttpPost]
    [HasPermission(Permissions.DeploymentManage)]
    [EnableRateLimiting(RateLimitPolicies.DeploymentAction)]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _webhooks.RegenerateSecretAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return this.ApiFailure(result, "Gizli anahtar üretilemedi.");

        return Ok(ApiResponse<ProjectWebhookSecretDto>.Success(result.Data, result.Data!.Message));
    }

    /// <summary>Webhook adresi; <c>Deployment:PublicBaseUrl</c> doluysa o, değilse isteğin adresi kullanılır.</summary>
    public static string WebhookUrl(Controller controller, DeploymentOptions options, Guid projectId)
    {
        var request = controller.Request;
        var baseUrl = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? $"{request.Scheme}://{request.Host}{request.PathBase}"
            : options.PublicBaseUrl.Trim().TrimEnd('/');
        return $"{baseUrl}/api/webhooks/projects/{projectId}";
    }
}

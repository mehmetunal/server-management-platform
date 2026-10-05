using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Deployments;
using ServerManager.Web.RateLimiting;

namespace ServerManager.Web.Controllers;

/// <summary>
/// Git sağlayıcısından gelen push webhook'u. Oturum yoktur; istek GitHub <c>X-Hub-Signature-256</c> (HMAC-SHA256) veya
/// GitLab <c>X-Gitlab-Token</c> ile projenin gizli anahtarına göre doğrulanır.
/// Cevaplar: 202 deploy başlatıldı veya kuyruğa alındı, 200 ping / yok sayılan olay veya dal, 401 imza geçersiz,
/// 403 otomatik deploy kapalı, 404 webhook yok, 409 kuyruğa alınamayan çakışma, 413 gövde çok büyük,
/// 422 deployment başlatılamadı (ör. proje ayarı hatası).
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/webhooks/projects")]
[EnableRateLimiting(RateLimitPolicies.ProjectWebhook)]
public class ProjectWebhookApiController : Controller
{
    private readonly IProjectWebhookService _webhooks;
    private readonly DeploymentManager _deploymentManager;
    private readonly ILogger<ProjectWebhookApiController> _logger;

    public ProjectWebhookApiController(IProjectWebhookService webhooks, DeploymentManager deploymentManager, ILogger<ProjectWebhookApiController> logger)
    {
        _webhooks = webhooks;
        _deploymentManager = deploymentManager;
        _logger = logger;
    }

    [HttpPost("{id:guid}")]
    [RequestSizeLimit(ProjectWebhooks.MaxBodyBytes)]
    public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken)
    {
        byte[] body;
        try
        {
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, cancellationToken);
            body = buffer.ToArray();
        }
        catch (BadHttpRequestException)
        {
            return Outcome(StatusCodes.Status413PayloadTooLarge, "İstek gövdesi çok büyük.");
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var delivery = new WebhookDelivery(
            body,
            Header(ProjectWebhooks.GitHubEventHeader),
            Header(ProjectWebhooks.GitHubSignatureHeader),
            Header(ProjectWebhooks.GitLabEventHeader),
            Header(ProjectWebhooks.GitLabTokenHeader),
            Header(ProjectWebhooks.GitHubDeliveryHeader) ?? Header(ProjectWebhooks.GitLabDeliveryHeader),
            remoteIp);

        var check = await _webhooks.CheckDeliveryAsync(id, delivery, cancellationToken);
        switch (check.Status)
        {
            case WebhookCheckStatus.NotFound:
                return Outcome(StatusCodes.Status404NotFound, check.Message);
            case WebhookCheckStatus.Unauthorized:
                return Outcome(StatusCodes.Status401Unauthorized, check.Message);
            case WebhookCheckStatus.Disabled:
                return Outcome(StatusCodes.Status403Forbidden, check.Message);
            case WebhookCheckStatus.Ping:
            case WebhookCheckStatus.Ignored:
                return Outcome(StatusCodes.Status200OK, check.Message);
        }

        var actor = new DeploymentActor(null, ProjectWebhooks.ActorName, remoteIp);
        var outcome = await _deploymentManager.StartFromWebhookAsync(id, check.Commit, actor, cancellationToken);
        var target = check.Commit is null ? string.Empty : $" ({GitRefs.ShortSha(check.Commit)})";
        var by = string.IsNullOrEmpty(check.Pusher) ? string.Empty : $", push: {check.Pusher}";
        var message = $"{outcome.Message}{target}{by}";
        await _webhooks.RecordDeliveryAsync(id, outcome.Status is WebhookDeployStatus.Started or WebhookDeployStatus.Queued, message, CancellationToken.None);
        _logger.LogInformation("Webhook işlendi. ProjectId: {ProjectId}, Status: {Status}, Delivery: {DeliveryId}", id, outcome.Status, delivery.DeliveryId);

        return outcome.Status switch
        {
            WebhookDeployStatus.Started or WebhookDeployStatus.Queued => Outcome(StatusCodes.Status202Accepted, message, outcome.DeploymentId),
            WebhookDeployStatus.Conflict => Outcome(StatusCodes.Status409Conflict, message),
            _ => Outcome(StatusCodes.Status422UnprocessableEntity, message)
        };
    }

    private string? Header(string name)
    {
        var value = Request.Headers[name].ToString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private ObjectResult Outcome(int statusCode, string message, Guid? deploymentId = null) =>
        StatusCode(statusCode, new { isSuccess = statusCode < 300, message, deploymentId });
}

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class ProjectWebhookService : IProjectWebhookService
{
    private const string NotFoundMessage = "Proje bulunamadı.";

    private readonly IDeploymentRepository _repository;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProjectWebhookService> _logger;

    public ProjectWebhookService(
        IDeploymentRepository repository,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<ProjectWebhookService> logger)
    {
        _repository = repository;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<ServiceResult<ProjectWebhookDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectWebhookDto>.NotFound(NotFoundMessage);

        return ServiceResult<ProjectWebhookDto>.Success(new ProjectWebhookDto
        {
            ProjectId = project.Id,
            AutoDeployOnPush = project.AutoDeployOnPush,
            HasSecret = project.EncryptedWebhookSecret is not null,
            Branch = project.Branch,
            LastDeliveryAt = project.WebhookLastDeliveryAt,
            LastDeliverySucceeded = project.WebhookLastDeliverySucceeded,
            LastDeliveryMessage = project.WebhookLastDeliveryMessage
        });
    }

    public async Task<ServiceResult<ProjectWebhookSecretDto>> SetAutoDeployAsync(Guid projectId, bool enabled, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectWebhookSecretDto>.NotFound(NotFoundMessage);

        string? secret = null;
        if (enabled && project.EncryptedWebhookSecret is null)
        {
            secret = ProjectWebhooks.GenerateSecret();
            project.EncryptedWebhookSecret = _secretProtector.Protect(secret);
        }

        if (project.AutoDeployOnPush == enabled && secret is null)
            return ServiceResult<ProjectWebhookSecretDto>.Success(new ProjectWebhookSecretDto(null, enabled ? "Otomatik deploy zaten açık." : "Otomatik deploy zaten kapalı."));

        project.AutoDeployOnPush = enabled;
        Touch(project);
        await _repository.SaveChangesAsync(cancellationToken);

        var details = enabled ? "Push ile otomatik deploy açıldı" + (secret is null ? string.Empty : "; yeni gizli anahtar üretildi") : "Push ile otomatik deploy kapatıldı";
        await AuditAsync(project, details, cancellationToken);
        var message = enabled
            ? secret is null ? "Push ile otomatik deploy açıldı." : "Push ile otomatik deploy açıldı. Gizli anahtarı şimdi kopyalayın; tekrar gösterilmez."
            : "Push ile otomatik deploy kapatıldı.";
        return ServiceResult<ProjectWebhookSecretDto>.Success(new ProjectWebhookSecretDto(secret, message), message);
    }

    public async Task<ServiceResult<ProjectWebhookSecretDto>> RegenerateSecretAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectWebhookSecretDto>.NotFound(NotFoundMessage);

        var secret = ProjectWebhooks.GenerateSecret();
        project.EncryptedWebhookSecret = _secretProtector.Protect(secret);
        Touch(project);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(project, "Webhook gizli anahtarı yeniden üretildi", cancellationToken);
        const string message = "Yeni gizli anahtar üretildi. Git sağlayıcısındaki webhook ayarını güncelleyin; eski anahtarla gelen istekler reddedilir.";
        return ServiceResult<ProjectWebhookSecretDto>.Success(new ProjectWebhookSecretDto(secret, message), message);
    }

    public async Task<WebhookCheckResult> CheckDeliveryAsync(Guid projectId, WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project?.EncryptedWebhookSecret is null)
            return new WebhookCheckResult(WebhookCheckStatus.NotFound, "Webhook bulunamadı.");

        string secret;
        try
        {
            secret = _secretProtector.Unprotect(project.EncryptedWebhookSecret);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Webhook gizli anahtarı çözülemedi. ProjectId: {ProjectId}", project.Id);
            await RecordAsync(project, false, "Gizli anahtar çözülemedi; panelden yeniden üretin.", cancellationToken);
            return new WebhookCheckResult(WebhookCheckStatus.Unauthorized, "İmza doğrulanamadı.");
        }

        if (!ProjectWebhooks.Verify(secret, delivery))
        {
            _logger.LogWarning("Webhook imzası doğrulanamadı. ProjectId: {ProjectId}, Source: {Source}, Ip: {Ip}", project.Id, delivery.Source, delivery.RemoteIp);
            var reason = delivery.Source == WebhookSource.Unknown
                ? "Tanınmayan istek: X-GitHub-Event veya X-Gitlab-Token başlığı yok."
                : $"{delivery.Source} imzası / belirteci doğrulanamadı; gizli anahtarı kontrol edin.";
            await RecordAsync(project, false, reason, cancellationToken);
            return new WebhookCheckResult(WebhookCheckStatus.Unauthorized, "İmza doğrulanamadı.");
        }

        var decision = ProjectWebhooks.Decide(delivery, project.Branch);
        if (decision.Kind == WebhookDecisionKind.Ping)
        {
            await RecordAsync(project, true, decision.Message, cancellationToken);
            return new WebhookCheckResult(WebhookCheckStatus.Ping, decision.Message);
        }

        if (!project.AutoDeployOnPush)
        {
            const string disabled = "Push ile otomatik deploy kapalı; teslimat yok sayıldı.";
            await RecordAsync(project, false, disabled, cancellationToken);
            return new WebhookCheckResult(WebhookCheckStatus.Disabled, disabled);
        }

        if (decision.Kind == WebhookDecisionKind.Ignore)
        {
            await RecordAsync(project, true, decision.Message, cancellationToken);
            return new WebhookCheckResult(WebhookCheckStatus.Ignored, decision.Message);
        }

        return new WebhookCheckResult(WebhookCheckStatus.Deploy, decision.Message, decision.Commit, decision.Pusher);
    }

    public async Task RecordDeliveryAsync(Guid projectId, bool succeeded, string message, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is not null)
            await RecordAsync(project, succeeded, message, cancellationToken);
    }

    private async Task RecordAsync(DeploymentProject project, bool succeeded, string message, CancellationToken cancellationToken)
    {
        project.WebhookLastDeliveryAt = UtcNow;
        project.WebhookLastDeliverySucceeded = succeeded;
        project.WebhookLastDeliveryMessage = TextHelper.Truncate(message, 500);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private void Touch(DeploymentProject project)
    {
        project.UpdatedAt = UtcNow;
        project.UpdatedBy = _currentUser.UserName;
    }

    private Task AuditAsync(DeploymentProject project, string details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            AuditActions.ProjectWebhookUpdate,
            AuditEntityTypes.Project,
            project.Id.ToString(),
            project.Name,
            details), cancellationToken);
}

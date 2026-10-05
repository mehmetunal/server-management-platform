using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>
/// Push ile otomatik deploy: proje başına webhook gizli anahtarı (şifreli), açma/kapama ve gelen teslimatın doğrulanması.
/// Deployment'ı başlatmak web katmanındaki deployment yöneticisinin işidir (elle deploy ile aynı yol).
/// </summary>
public interface IProjectWebhookService
{
    Task<ServiceResult<ProjectWebhookDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Açar veya kapatır. İlk açılışta gizli anahtar üretilir ve yalnızca bu cevapta döner.</summary>
    Task<ServiceResult<ProjectWebhookSecretDto>> SetAutoDeployAsync(Guid projectId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Yeni gizli anahtar üretir; eskisiyle imzalanan teslimatlar artık reddedilir.</summary>
    Task<ServiceResult<ProjectWebhookSecretDto>> RegenerateSecretAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Teslimatı doğrular (sabit zamanlı) ve olay/dal filtresini uygular. Doğrulanan teslimatlar projeye "son teslimat" olarak yazılır.</summary>
    Task<WebhookCheckResult> CheckDeliveryAsync(Guid projectId, WebhookDelivery delivery, CancellationToken cancellationToken = default);

    /// <summary>Deploy başlatma sonucunu son teslimat durumu olarak yazar.</summary>
    Task RecordDeliveryAsync(Guid projectId, bool succeeded, string message, CancellationToken cancellationToken = default);
}

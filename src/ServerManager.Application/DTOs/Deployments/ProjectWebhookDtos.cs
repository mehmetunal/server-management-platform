namespace ServerManager.Application.DTOs.Deployments;

public sealed class ProjectWebhookDto
{
    public Guid ProjectId { get; init; }

    public bool AutoDeployOnPush { get; init; }

    public bool HasSecret { get; init; }

    public string Branch { get; init; } = string.Empty;

    public DateTime? LastDeliveryAt { get; init; }

    public bool? LastDeliverySucceeded { get; init; }

    public string? LastDeliveryMessage { get; init; }
}

/// <summary>Yeni üretilen gizli anahtar; yalnızca bu cevapta görünür, sonra yalnızca yeniden üretilebilir.</summary>
public sealed record ProjectWebhookSecretDto(string? Secret, string Message);

public enum WebhookCheckStatus
{
    /// <summary>Proje yok veya webhook anahtarı hiç üretilmemiş.</summary>
    NotFound,

    /// <summary>İmza / belirteç doğrulanamadı.</summary>
    Unauthorized,

    /// <summary>İmza doğru ancak projede "Push ile otomatik deploy" kapalı.</summary>
    Disabled,

    Ping,

    /// <summary>Push dışındaki olay veya başka dal.</summary>
    Ignored,

    Deploy
}

public sealed record WebhookCheckResult(WebhookCheckStatus Status, string Message, string? Commit = null, string? Pusher = null);

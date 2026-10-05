namespace ServerManager.Web.Deployments;

public enum WebhookDeployStatus
{
    Started,

    /// <summary>Süren deployment bitince dalın son hali deploy edilecek.</summary>
    Queued,

    /// <summary>Başka bir uygulama örneğinde veya yarım kalmış kayıtta süren deployment var; kuyruğa alınamadı.</summary>
    Conflict,

    Failed
}

public sealed record WebhookDeployOutcome(WebhookDeployStatus Status, Guid? DeploymentId, string Message);

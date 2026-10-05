using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Web.Models;

public sealed class WebhookPanelViewModel
{
    public required ProjectWebhookDto Webhook { get; init; }

    public required string Url { get; init; }

    public bool CanManage { get; init; }

    public static WebhookPanelViewModel Create(ProjectWebhookDto webhook, string url, bool canManage) =>
        new() { Webhook = webhook, Url = url, CanManage = canManage };
}

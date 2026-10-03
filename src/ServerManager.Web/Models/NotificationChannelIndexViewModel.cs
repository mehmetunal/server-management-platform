using ServerManager.Application.DTOs.Alerting;

namespace ServerManager.Web.Models;

public sealed class NotificationChannelIndexViewModel
{
    public required IReadOnlyList<NotificationChannelListItemDto> Channels { get; init; }

    public required IReadOnlyList<NotificationDeliveryDto> Deliveries { get; init; }

    public required IReadOnlyList<NotificationProviderDto> Providers { get; init; }
}

using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class NotificationDelivery
{
    public long Id { get; set; }

    public Guid ChannelId { get; set; }

    public string ChannelName { get; set; } = string.Empty;

    public Guid? AlertEventId { get; set; }

    public NotificationKind Kind { get; set; }

    public bool IsSuccess { get; set; }

    public string? Message { get; set; }

    public DateTime SentAt { get; set; }
}

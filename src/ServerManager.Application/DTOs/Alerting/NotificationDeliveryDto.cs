using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed record NotificationDeliveryDto(string ChannelName, NotificationKind Kind, bool IsSuccess, string? Message, DateTime SentAt);

using ServerManager.Application.Notifications;

namespace ServerManager.Application.DTOs.Alerting;

public sealed record NotificationProviderDto(string SystemName, string DisplayName, string Description, IReadOnlyList<NotificationSettingField> Fields);

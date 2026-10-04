using ServerManager.Application.Notifications;

namespace ServerManager.Application.DTOs.Backups;

public sealed record BackupStorageProviderDto(
    string SystemName,
    string DisplayName,
    string Description,
    IReadOnlyList<NotificationSettingField> Fields);

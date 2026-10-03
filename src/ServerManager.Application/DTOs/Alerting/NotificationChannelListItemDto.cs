using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed record NotificationChannelListItemDto(
    Guid Id,
    string Name,
    string ProviderSystemName,
    string ProviderName,
    bool ProviderAvailable,
    AlertSeverity MinimumSeverity,
    bool IsEnabled,
    DateTime? LastSentAt,
    string? LastError,
    int RuleCount);

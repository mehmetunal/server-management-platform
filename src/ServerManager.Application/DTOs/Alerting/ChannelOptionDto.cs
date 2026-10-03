using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Alerting;

public sealed record ChannelOptionDto(Guid Id, string Name, string ProviderName, bool IsEnabled, AlertSeverity MinimumSeverity);

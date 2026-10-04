namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudAccountListItemDto(
    Guid Id,
    string Name,
    string Provider,
    string ProviderName,
    bool IsProviderEnabled,
    string? AccountLabel,
    DateTime? LastSyncAt,
    string? LastSyncError,
    int LinkedServerCount,
    IReadOnlyDictionary<string, decimal> MonthlyCosts);

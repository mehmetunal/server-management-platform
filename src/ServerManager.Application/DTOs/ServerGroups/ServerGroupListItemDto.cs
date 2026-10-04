namespace ServerManager.Application.DTOs.ServerGroups;

public sealed record ServerGroupListItemDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    int ServerCount,
    int OnlineCount,
    IReadOnlyDictionary<string, decimal> MonthlyCosts,
    IReadOnlyList<string> ServerNames);

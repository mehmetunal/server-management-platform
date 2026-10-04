namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudServerInfo(
    string ExternalId,
    string Name,
    string Status,
    string? PublicIpv4,
    string? Region,
    string? Size,
    decimal? MonthlyPrice,
    string? Currency,
    DateTime? CreatedAt);

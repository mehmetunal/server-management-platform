namespace ServerManager.Application.DTOs.Costs;

public sealed record CostServerDto(Guid Id, string Name, string? GroupName, string? GroupColor, string Source, decimal? MonthlyCost, string? Currency);

namespace ServerManager.Application.DTOs.Costs;

public sealed record CostBreakdownDto(string Name, string? Color, int ServerCount, int CostedServerCount, IReadOnlyDictionary<string, decimal> MonthlyCosts);

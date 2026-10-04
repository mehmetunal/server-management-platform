namespace ServerManager.Application.DTOs.Costs;

public sealed record CostReportDto(
    IReadOnlyDictionary<string, decimal> MonthlyTotals,
    int ServerCount,
    int CostedServerCount,
    IReadOnlyList<CostBreakdownDto> ByGroup,
    IReadOnlyList<CostBreakdownDto> BySource,
    IReadOnlyList<CostServerDto> Servers);

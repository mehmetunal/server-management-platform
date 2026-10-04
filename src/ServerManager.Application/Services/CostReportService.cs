using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Costs;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class CostReportService : ICostReportService
{
    public const string ManualSource = "Elle girilen";
    public const string NoGroup = "Grupsuz";

    private readonly IServerRepository _serverRepository;

    public CostReportService(IServerRepository serverRepository)
    {
        _serverRepository = serverRepository;
    }

    public async Task<CostReportDto> GetReportAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.GetAllForCostReportAsync(cancellationToken);

        var byGroup = servers
            .GroupBy(s => s.Group is null ? null : (Guid?)s.Group.Id)
            .Select(g => Breakdown(g.First().Group?.Name ?? NoGroup, g.First().Group?.Color, g.ToList()))
            .OrderBy(b => b.Name == NoGroup)
            .ThenBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var bySource = servers
            .GroupBy(Source)
            .Select(g => Breakdown(g.Key, null, g.ToList()))
            .OrderBy(b => b.Name == ManualSource)
            .ThenBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var rows = servers
            .OrderByDescending(s => s.MonthlyCost.HasValue)
            .ThenBy(s => s.CostCurrency)
            .ThenByDescending(s => s.MonthlyCost)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new CostServerDto(s.Id, s.Name, s.Group?.Name, s.Group?.Color, Source(s), s.MonthlyCost, s.MonthlyCost is null ? null : s.CostCurrency ?? CostCurrencies.Default))
            .ToList();

        return new CostReportDto(Totals(servers), servers.Count, servers.Count(s => s.MonthlyCost.HasValue), byGroup, bySource, rows);
    }

    private static string Source(Server server) =>
        server.CloudAccount is { } account ? account.Name : ManualSource;

    private static CostBreakdownDto Breakdown(string name, string? color, IReadOnlyList<Server> servers) =>
        new(name, color, servers.Count, servers.Count(s => s.MonthlyCost.HasValue), Totals(servers));

    private static IReadOnlyDictionary<string, decimal> Totals(IEnumerable<Server> servers) =>
        servers
            .Where(s => s.MonthlyCost.HasValue)
            .GroupBy(s => s.CostCurrency ?? CostCurrencies.Default)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.MonthlyCost!.Value));
}

using NSubstitute;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Tests.Services;

public class CostReportServiceTests
{
    private readonly IServerRepository _repository = Substitute.For<IServerRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetReport_groups_costs_by_currency_group_and_source()
    {
        var group = new ServerGroup { Name = "Web", Color = "blue" };
        var account = new CloudAccount { Name = "Hetzner ana", Provider = "Cloud.Hetzner", EncryptedToken = "x" };
        _repository.GetAllForCostReportAsync(Arg.Any<CancellationToken>()).Returns(new List<Server>
        {
            new() { Name = "web-01", Group = group, CloudAccount = account, MonthlyCost = 4.5m, CostCurrency = "EUR" },
            new() { Name = "web-02", Group = group, MonthlyCost = 10m, CostCurrency = "USD" },
            new() { Name = "db-01", MonthlyCost = 20m, CostCurrency = "EUR" },
            new() { Name = "eski" }
        });

        var report = await new CostReportService(_repository).GetReportAsync(Ct);

        Assert.Equal(24.5m, report.MonthlyTotals["EUR"]);
        Assert.Equal(10m, report.MonthlyTotals["USD"]);
        Assert.Equal(4, report.ServerCount);
        Assert.Equal(3, report.CostedServerCount);

        Assert.Equal(["Web", CostReportService.NoGroup], report.ByGroup.Select(g => g.Name));
        Assert.Equal(4.5m, report.ByGroup[0].MonthlyCosts["EUR"]);
        Assert.Equal(1, report.ByGroup[1].CostedServerCount);
        Assert.Equal(2, report.ByGroup[1].ServerCount);

        Assert.Equal(["Hetzner ana", CostReportService.ManualSource], report.BySource.Select(s => s.Name));
        Assert.Equal(20m, report.BySource[1].MonthlyCosts["EUR"]);

        Assert.Equal("eski", report.Servers[^1].Name);
        Assert.Null(report.Servers[^1].Currency);
        Assert.Equal("db-01", report.Servers[0].Name);
    }

    [Fact]
    public async Task GetReport_uses_default_currency_when_missing()
    {
        _repository.GetAllForCostReportAsync(Arg.Any<CancellationToken>()).Returns(new List<Server>
        {
            new() { Name = "a", MonthlyCost = 5m }
        });

        var report = await new CostReportService(_repository).GetReportAsync(Ct);

        Assert.Equal(5m, report.MonthlyTotals["USD"]);
        Assert.Equal("USD", report.Servers[0].Currency);
    }
}

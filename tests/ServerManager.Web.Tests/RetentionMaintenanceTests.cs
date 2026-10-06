using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Monitoring;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>
/// Saklama süresi temizliği ham SQL ve oturuma bağlı geçici tablo (#RetentionKeep) kullanır; bu davranış yalnızca gerçek
/// SQL Server'da doğrulanabilir (ör. sp_executesql içinde açılan geçici tablonun komut bitince düşmesi).
/// </summary>
[Collection(WebCollection.Name)]
public sealed class RetentionMaintenanceTests(ServerManagerWebFactory factory)
{
    /// <summary>Bu hedefler kendi depolarından silinir; metrik deposu onları bilerek reddeder.</summary>
    private static readonly RetentionTarget[] OwnRepositoryTargets =
        [RetentionTarget.SecurityScans, RetentionTarget.UptimeResults, RetentionTarget.NotificationDeliveries];

    public static TheoryData<RetentionTarget> Targets()
    {
        var data = new TheoryData<RetentionTarget>();
        foreach (var target in Enum.GetValues<RetentionTarget>().Except(OwnRepositoryTargets))
            data.Add(target);
        return data;
    }

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task Every_retention_target_runs_against_sql_server(RetentionTarget target)
    {
        using var client = factory.CreateTestClient();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServerMetricRepository>();

        var deleted = await repository.DeleteExpiredAsync(target, DateTime.UtcNow, keepLatestPerServer: 1, TestContext.Current.CancellationToken);

        Assert.True(deleted >= 0);
    }

    [Fact]
    public async Task Repository_owned_retention_targets_run_against_sql_server()
    {
        using var client = factory.CreateTestClient();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await services.GetRequiredService<ISecurityScanRepository>().DeleteExpiredAsync(DateTime.UtcNow, 1, cancellationToken) >= 0);
        Assert.True(await services.GetRequiredService<IUptimeRepository>().DeleteExpiredResultsAsync(DateTime.UtcNow, 1, cancellationToken) >= 0);
        Assert.True(await services.GetRequiredService<IAlertRepository>().DeleteExpiredDeliveriesAsync(DateTime.UtcNow, 1, cancellationToken) >= 0);
    }
}

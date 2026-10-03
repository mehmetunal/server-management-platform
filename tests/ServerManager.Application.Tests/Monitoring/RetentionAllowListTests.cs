using ServerManager.Application.Monitoring;
using ServerManager.Infrastructure.Repositories;

namespace ServerManager.Application.Tests.Monitoring;

public class RetentionAllowListTests
{
    [Theory]
    [InlineData(RetentionTarget.RawMetrics, "ServerMetrics", "CollectedAt")]
    [InlineData(RetentionTarget.HourlyMetrics, "ServerMetricsHourly", "HourStart")]
    [InlineData(RetentionTarget.HealthChecks, "ServerHealthChecks", "CheckedAt")]
    public void Resolves_only_temporary_monitoring_tables(RetentionTarget target, string table, string column)
    {
        var mapping = RetentionAllowList.Resolve(target);

        Assert.Equal(table, mapping.Table);
        Assert.Equal(column, mapping.TimeColumn);
    }

    [Theory]
    [InlineData("Servers")]
    [InlineData("ServerCredentials")]
    [InlineData("ServerMetricSnapshots")]
    [InlineData("AuditLogs")]
    [InlineData("AspNetUsers")]
    [InlineData("ServerMetrics; DROP TABLE Servers")]
    [InlineData("serverMetrics")]
    [InlineData("")]
    public void Throws_for_tables_outside_allow_list(string table)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RetentionAllowList.EnsureAllowed(table, "CollectedAt"));
        Assert.Contains("izin listesinde değil", exception.Message);
    }

    [Theory]
    [InlineData("CreatedAt")]
    [InlineData("CollectedAt OR 1=1")]
    public void Throws_for_columns_outside_allow_list(string column)
    {
        Assert.Throws<InvalidOperationException>(() => RetentionAllowList.EnsureAllowed("ServerMetrics", column));
    }

    [Fact]
    public void Throws_for_unknown_target()
    {
        Assert.Throws<InvalidOperationException>(() => RetentionAllowList.Resolve((RetentionTarget)99));
    }
}

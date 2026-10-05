using ServerManager.Application.Monitoring;
using ServerManager.Infrastructure.Repositories;

namespace ServerManager.Application.Tests.Monitoring;

public class RetentionAllowListTests
{
    [Theory]
    [InlineData(RetentionTarget.RawMetrics, "ServerMetrics", "CollectedAt", "ServerId")]
    [InlineData(RetentionTarget.HourlyMetrics, "ServerMetricsHourly", "HourStart", "ServerId")]
    [InlineData(RetentionTarget.HealthChecks, "ServerHealthChecks", "CheckedAt", "ServerId")]
    [InlineData(RetentionTarget.UptimeResults, "UptimeCheckResults", "CheckedAt", "CheckId")]
    [InlineData(RetentionTarget.NotificationDeliveries, "NotificationDeliveries", "SentAt", "ChannelId")]
    [InlineData(RetentionTarget.SecurityScans, "SecurityScans", "StartedAt", "ServerId")]
    public void Resolves_only_temporary_monitoring_tables(RetentionTarget target, string table, string column, string partition)
    {
        var mapping = RetentionAllowList.Resolve(target);

        Assert.Equal(table, mapping.Table);
        Assert.Equal(column, mapping.TimeColumn);
        Assert.Equal(partition, mapping.PartitionColumn);
    }

    [Theory]
    [InlineData("UptimeChecks")]
    [InlineData("SslMonitors")]
    [InlineData("AlertRules")]
    [InlineData("NotificationChannels")]
    public void Alerting_configuration_tables_are_never_deletable(string table)
    {
        Assert.Throws<InvalidOperationException>(() => RetentionAllowList.EnsureAllowed(table, "CheckedAt"));
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("ServerId, 1")]
    public void Throws_for_partition_columns_outside_allow_list(string partition)
    {
        Assert.Throws<InvalidOperationException>(() => RetentionAllowList.EnsureAllowed("UptimeCheckResults", "CheckedAt", partition));
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

    [Theory]
    [InlineData(RetentionTarget.DeploymentLogs, "Deployments", "StartedAt", "ProjectId")]
    [InlineData(RetentionTarget.BackupRunLogs, "BackupRuns", "StartedAt", "JobId")]
    public void Log_targets_trim_log_column_and_protect_latest_per_partition(RetentionTarget target, string table, string column, string partition)
    {
        var mapping = RetentionAllowList.Resolve(target);

        Assert.Equal(RetentionMode.TrimLog, mapping.Mode);
        Assert.Equal((table, column, partition, "Log"), (mapping.Table, mapping.TimeColumn, mapping.PartitionColumn, mapping.LogColumn));
        Assert.Contains("CompletedAt IS NOT NULL", mapping.Filter);
    }

    [Theory]
    [InlineData(RetentionTarget.CommandRuns, "CommandRuns", "CommandRunTargets", "RunId")]
    [InlineData(RetentionTarget.TerminalSessions, "TerminalSessions", "TerminalCommands", "SessionId")]
    public void History_targets_delete_children_first(RetentionTarget target, string table, string child, string foreignKey)
    {
        var mapping = RetentionAllowList.Resolve(target);
        var sql = RetentionDeleter.BuildDeleteWithChildrenSql(mapping);

        Assert.Equal(RetentionMode.DeleteWithChildren, mapping.Mode);
        Assert.Equal((table, child, foreignKey), (mapping.Table, mapping.ChildTable, mapping.ChildForeignKey));
        Assert.True(sql.IndexOf($"DELETE c FROM {child}", StringComparison.Ordinal) < sql.IndexOf($"DELETE t FROM {table}", StringComparison.Ordinal));
    }

    [Fact]
    public void Alert_events_only_delete_resolved_rows()
    {
        var mapping = RetentionAllowList.Resolve(RetentionTarget.AlertEvents);

        Assert.Equal("ResolvedAt", mapping.TimeColumn);
        Assert.Equal("t.Status = 2", mapping.Filter);
    }

    [Fact]
    public void No_target_maps_to_audit_logs()
    {
        foreach (var target in Enum.GetValues<RetentionTarget>())
        {
            var mapping = RetentionAllowList.Resolve(target);
            Assert.NotEqual("AuditLogs", mapping.Table);
            Assert.NotEqual("AuditLogs", mapping.ChildTable);
        }
    }

    [Fact]
    public void Delete_uses_precomputed_keep_table_instead_of_per_batch_row_number()
    {
        var mapping = RetentionAllowList.Resolve(RetentionTarget.RawMetrics);

        var keep = RetentionDeleter.BuildKeepSql(mapping);
        var delete = RetentionDeleter.BuildDeleteSql(mapping, protect: true);

        Assert.Contains("ROW_NUMBER()", keep);
        Assert.Contains("#RetentionKeep", keep);
        Assert.DoesNotContain("ROW_NUMBER", delete);
        Assert.Contains("NOT EXISTS (SELECT 1 FROM #RetentionKeep k WHERE k.Id = t.Id)", delete);
    }

    [Fact]
    public void Trim_only_touches_logs_longer_than_marker()
    {
        var sql = RetentionDeleter.BuildTrimSql(RetentionAllowList.Resolve(RetentionTarget.DeploymentLogs), protect: true);

        Assert.Contains("SET t.Log = @marker", sql);
        Assert.Contains("DATALENGTH(t.Log) > DATALENGTH(@marker)", sql);
        Assert.Contains("t.CompletedAt IS NOT NULL", sql);
    }

    [Fact]
    public void Throws_for_unknown_target()
    {
        Assert.Throws<InvalidOperationException>(() => RetentionAllowList.Resolve((RetentionTarget)99));
    }
}

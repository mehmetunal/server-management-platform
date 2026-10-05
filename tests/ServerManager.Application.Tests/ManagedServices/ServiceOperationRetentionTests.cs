using ServerManager.Application.Monitoring;
using ServerManager.Infrastructure.Repositories;

namespace ServerManager.Application.Tests.ManagedServices;

public class ServiceOperationRetentionTests
{
    [Fact]
    public void Service_operation_logs_are_trimmed_not_deleted()
    {
        var mapping = RetentionAllowList.Resolve(RetentionTarget.ServiceOperationLogs);

        Assert.Equal(RetentionMode.TrimLog, mapping.Mode);
        Assert.Equal(("ManagedServiceOperations", "StartedAt", "ServiceId", "Log"), (mapping.Table, mapping.TimeColumn, mapping.PartitionColumn, mapping.LogColumn));
        Assert.Equal("t.FinishedAt IS NOT NULL", mapping.Filter);

        var sql = RetentionDeleter.BuildTrimSql(mapping, protect: true);
        Assert.Contains("SET t.Log = @marker", sql);
        Assert.Contains("t.FinishedAt IS NOT NULL", sql);
    }

    [Fact]
    public void Default_retention_is_90_days()
    {
        Assert.Equal(90, new RetentionOptions().ServiceOperationLogDays);
        Assert.Equal(10, RetentionOptions.ProtectedServiceOperationLogsPerService);
    }
}

using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030004, "Monitoring tabloları ve sunucu izleme alanları")]
public class M004_MonitoringSchema : Migration
{
    public override void Up()
    {
        Alter.Table("Servers")
            .AddColumn("MonitoringEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .AddColumn("LastSeenAt").AsDateTime2().Nullable()
            .AddColumn("ConsecutiveFailureCount").AsInt32().NotNullable().WithDefaultValue(0);

        Create.Table("ServerMetrics")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_ServerMetrics").Identity()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ServerMetrics_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("CollectedAt").AsDateTime2().NotNullable()
            .WithColumn("CpuUsagePercent").AsDouble().NotNullable()
            .WithColumn("LoadAverage1").AsDouble().NotNullable()
            .WithColumn("LoadAverage5").AsDouble().NotNullable()
            .WithColumn("LoadAverage15").AsDouble().NotNullable()
            .WithColumn("MemoryTotalBytes").AsInt64().NotNullable()
            .WithColumn("MemoryUsedBytes").AsInt64().NotNullable()
            .WithColumn("MemoryUsagePercent").AsDouble().NotNullable()
            .WithColumn("SwapTotalBytes").AsInt64().NotNullable()
            .WithColumn("SwapUsedBytes").AsInt64().NotNullable()
            .WithColumn("DiskTotalBytes").AsInt64().NotNullable()
            .WithColumn("DiskUsedBytes").AsInt64().NotNullable()
            .WithColumn("DiskUsagePercent").AsDouble().NotNullable()
            .WithColumn("NetworkRxBytesPerSecond").AsDouble().NotNullable()
            .WithColumn("NetworkTxBytesPerSecond").AsDouble().NotNullable()
            .WithColumn("UptimeSeconds").AsInt64().NotNullable();

        Create.Index("IX_ServerMetrics_ServerId_CollectedAt").OnTable("ServerMetrics")
            .OnColumn("ServerId").Ascending()
            .OnColumn("CollectedAt").Descending();
        Create.Index("IX_ServerMetrics_CollectedAt").OnTable("ServerMetrics").OnColumn("CollectedAt").Ascending();

        Create.Table("ServerMetricsHourly")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_ServerMetricsHourly").Identity()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ServerMetricsHourly_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("HourStart").AsDateTime2().NotNullable()
            .WithColumn("SampleCount").AsInt32().NotNullable()
            .WithColumn("CpuUsagePercentAvg").AsDouble().NotNullable()
            .WithColumn("CpuUsagePercentMax").AsDouble().NotNullable()
            .WithColumn("MemoryUsagePercentAvg").AsDouble().NotNullable()
            .WithColumn("MemoryUsagePercentMax").AsDouble().NotNullable()
            .WithColumn("DiskUsagePercentAvg").AsDouble().NotNullable()
            .WithColumn("DiskUsagePercentMax").AsDouble().NotNullable()
            .WithColumn("NetworkRxBytesPerSecondAvg").AsDouble().NotNullable()
            .WithColumn("NetworkTxBytesPerSecondAvg").AsDouble().NotNullable()
            .WithColumn("LoadAverage1Avg").AsDouble().NotNullable();

        Create.Index("UX_ServerMetricsHourly_ServerId_HourStart").OnTable("ServerMetricsHourly")
            .OnColumn("ServerId").Ascending()
            .OnColumn("HourStart").Ascending()
            .WithOptions().Unique();

        Create.Table("ServerHealthChecks")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_ServerHealthChecks").Identity()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ServerHealthChecks_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("CheckedAt").AsDateTime2().NotNullable()
            .WithColumn("IsSuccess").AsBoolean().NotNullable()
            .WithColumn("ResponseTimeMs").AsInt64().NotNullable()
            .WithColumn("Message").AsString(500).Nullable();

        Create.Index("IX_ServerHealthChecks_ServerId_CheckedAt").OnTable("ServerHealthChecks")
            .OnColumn("ServerId").Ascending()
            .OnColumn("CheckedAt").Descending();

        Create.Table("ServerMetricSnapshots")
            .WithColumn("ServerId").AsGuid().PrimaryKey("PK_ServerMetricSnapshots")
                .ForeignKey("FK_ServerMetricSnapshots_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("CollectedAt").AsDateTime2().NotNullable()
            .WithColumn("SnapshotJson").AsString(int.MaxValue).NotNullable();
    }

    public override void Down()
    {
        Delete.Table("ServerMetricSnapshots");
        Delete.Table("ServerHealthChecks");
        Delete.Table("ServerMetricsHourly");
        Delete.Table("ServerMetrics");
        Delete.Column("ConsecutiveFailureCount").FromTable("Servers");
        Delete.Column("LastSeenAt").FromTable("Servers");
        Delete.Column("MonitoringEnabled").FromTable("Servers");
    }
}

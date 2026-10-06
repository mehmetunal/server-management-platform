using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610060028, "Kalıcı kuyruklar (webhook takip deploy'u, kurulum sonrası yedek), kaynak geçmişi ve servis alarmları")]
public class M028_PersistentQueuesHistoryAlerts : Migration
{
    public override void Up()
    {
        // Webhook takip deploy'u: uygulama yeniden başlasa da kaybolmaz.
        Alter.Table("DeploymentProjects")
            .AddColumn("PendingWebhookDeployAt").AsDateTime2().Nullable()
            .AddColumn("PendingWebhookCommit").AsString(64).Nullable()
            .AddColumn("PendingWebhookIpAddress").AsString(64).Nullable();

        Create.Index("IX_DeploymentProjects_PendingWebhookDeployAt").OnTable("DeploymentProjects")
            .OnColumn("PendingWebhookDeployAt").Ascending();

        // Kurulum sonrası otomatik yedek isteği (şifreli JSON).
        Alter.Table("ManagedServiceOperations")
            .AddColumn("PendingAutoBackup").AsString(int.MaxValue).Nullable();

        // Servis kuralları tek bir servise daraltılabilir.
        Alter.Table("AlertRules")
            .AddColumn("ManagedServiceId").AsGuid().Nullable();

        Create.Table("ContainerMetricSamples")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_ContainerMetricSamples").Identity()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ContainerMetricSamples_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("CollectedAt").AsDateTime2().NotNullable()
            .WithColumn("ContainerName").AsString(256).NotNullable()
            .WithColumn("State").AsString(32).NotNullable()
            .WithColumn("Health").AsString(32).Nullable()
            .WithColumn("RestartCount").AsInt32().NotNullable()
            .WithColumn("CpuPercent").AsDouble().NotNullable()
            .WithColumn("MemoryUsageBytes").AsInt64().NotNullable()
            .WithColumn("MemoryLimitBytes").AsInt64().NotNullable()
            .WithColumn("MemoryPercent").AsDouble().NotNullable()
            .WithColumn("NetworkRxBytes").AsInt64().NotNullable()
            .WithColumn("NetworkTxBytes").AsInt64().NotNullable()
            .WithColumn("BlockReadBytes").AsInt64().NotNullable()
            .WithColumn("BlockWriteBytes").AsInt64().NotNullable();

        Create.Index("IX_ContainerMetricSamples_ServerId_CollectedAt").OnTable("ContainerMetricSamples")
            .OnColumn("ServerId").Ascending()
            .OnColumn("CollectedAt").Descending();
        Create.Index("IX_ContainerMetricSamples_CollectedAt").OnTable("ContainerMetricSamples").OnColumn("CollectedAt").Ascending();

        Create.Table("ContainerMetricsHourly")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_ContainerMetricsHourly").Identity()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ContainerMetricsHourly_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ContainerName").AsString(256).NotNullable()
            .WithColumn("HourStart").AsDateTime2().NotNullable()
            .WithColumn("SampleCount").AsInt32().NotNullable()
            .WithColumn("CpuPercentAvg").AsDouble().NotNullable()
            .WithColumn("CpuPercentMax").AsDouble().NotNullable()
            .WithColumn("MemoryUsageBytesAvg").AsDouble().NotNullable()
            .WithColumn("MemoryUsageBytesMax").AsInt64().NotNullable()
            .WithColumn("RestartCountMax").AsInt32().NotNullable();

        Create.Index("UX_ContainerMetricsHourly_ServerId_HourStart_ContainerName").OnTable("ContainerMetricsHourly")
            .OnColumn("ServerId").Ascending()
            .OnColumn("HourStart").Ascending()
            .OnColumn("ContainerName").Ascending()
            .WithOptions().Unique();

        Create.Table("ProcessSnapshots")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_ProcessSnapshots").Identity()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ProcessSnapshots_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("CollectedAt").AsDateTime2().NotNullable()
            .WithColumn("CpuBusyPercent").AsDouble().Nullable()
            .WithColumn("ProcessesJson").AsString(int.MaxValue).NotNullable();

        Create.Index("IX_ProcessSnapshots_ServerId_CollectedAt").OnTable("ProcessSnapshots")
            .OnColumn("ServerId").Ascending()
            .OnColumn("CollectedAt").Descending();

        Create.Table("ServerReclaimableSpace")
            .WithColumn("ServerId").AsGuid().PrimaryKey("PK_ServerReclaimableSpace")
                .ForeignKey("FK_ServerReclaimableSpace_Servers", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ScannedAt").AsDateTime2().NotNullable()
            .WithColumn("ReclaimableBytes").AsInt64().NotNullable()
            .WithColumn("SafeReclaimableBytes").AsInt64().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("ServerReclaimableSpace");
        Delete.Table("ProcessSnapshots");
        Delete.Table("ContainerMetricsHourly");
        Delete.Table("ContainerMetricSamples");

        Delete.Column("ManagedServiceId").FromTable("AlertRules");
        Delete.Column("PendingAutoBackup").FromTable("ManagedServiceOperations");

        Delete.Index("IX_DeploymentProjects_PendingWebhookDeployAt").OnTable("DeploymentProjects");
        Delete.Column("PendingWebhookDeployAt")
            .Column("PendingWebhookCommit")
            .Column("PendingWebhookIpAddress")
            .FromTable("DeploymentProjects");
    }
}

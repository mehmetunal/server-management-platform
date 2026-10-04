using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040010, "Alarm kuralları, bildirim kanalları, uptime ve SSL izleme tabloları")]
public class M010_AlertingSchema : Migration
{
    public override void Up()
    {
        Create.Table("NotificationChannels")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_NotificationChannels")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("ProviderSystemName").AsString(100).NotNullable()
            .WithColumn("EncryptedSettings").AsString(int.MaxValue).NotNullable()
            .WithColumn("MinimumSeverity").AsInt32().NotNullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("LastSentAt").AsDateTime2().Nullable()
            .WithColumn("LastError").AsString(500).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
        Execute.Sql("CREATE UNIQUE INDEX UX_NotificationChannels_Name ON NotificationChannels (Name) WHERE IsDeleted = 0;");

        Create.Table("AlertRules")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_AlertRules")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("Severity").AsInt32().NotNullable()
            .WithColumn("Threshold").AsDouble().NotNullable().WithDefaultValue(0)
            .WithColumn("DurationMinutes").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ServerId").AsGuid().Nullable()
                .ForeignKey("FK_AlertRules_Servers", "Servers", "Id")
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("NotifyRecovery").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("RepeatIntervalMinutes").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
        Create.Index("IX_AlertRules_ServerId").OnTable("AlertRules").OnColumn("ServerId");

        Create.Table("AlertRuleChannels")
            .WithColumn("RuleId").AsGuid().NotNullable().PrimaryKey("PK_AlertRuleChannels")
                .ForeignKey("FK_AlertRuleChannels_AlertRules", "AlertRules", "Id")
            .WithColumn("ChannelId").AsGuid().NotNullable().PrimaryKey("PK_AlertRuleChannels")
                .ForeignKey("FK_AlertRuleChannels_NotificationChannels", "NotificationChannels", "Id");
        Create.Index("IX_AlertRuleChannels_ChannelId").OnTable("AlertRuleChannels").OnColumn("ChannelId");

        Create.Table("AlertEvents")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_AlertEvents")
            .WithColumn("RuleId").AsGuid().NotNullable()
                .ForeignKey("FK_AlertEvents_AlertRules", "AlertRules", "Id")
            .WithColumn("RuleName").AsString(128).NotNullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("Severity").AsInt32().NotNullable()
            .WithColumn("ServerId").AsGuid().Nullable()
            .WithColumn("ServerName").AsString(256).Nullable()
            .WithColumn("TargetKey").AsString(100).NotNullable()
            .WithColumn("TargetName").AsString(256).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("Message").AsString(1000).NotNullable()
            .WithColumn("Value").AsDouble().Nullable()
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("ResolvedAt").AsDateTime2().Nullable()
            .WithColumn("ResolvedMessage").AsString(1000).Nullable()
            .WithColumn("LastNotifiedAt").AsDateTime2().Nullable()
            .WithColumn("NotifiedValue").AsDouble().Nullable()
            .WithColumn("AcknowledgedAt").AsDateTime2().Nullable()
            .WithColumn("AcknowledgedBy").AsString(256).Nullable();
        Create.Index("IX_AlertEvents_Status_StartedAt").OnTable("AlertEvents")
            .OnColumn("Status").Ascending()
            .OnColumn("StartedAt").Descending();
        Create.Index("IX_AlertEvents_RuleId_Status").OnTable("AlertEvents")
            .OnColumn("RuleId").Ascending()
            .OnColumn("Status").Ascending();
        Create.Index("IX_AlertEvents_ServerId_StartedAt").OnTable("AlertEvents")
            .OnColumn("ServerId").Ascending()
            .OnColumn("StartedAt").Descending();

        Create.Table("NotificationDeliveries")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_NotificationDeliveries").Identity()
            .WithColumn("ChannelId").AsGuid().NotNullable()
            .WithColumn("ChannelName").AsString(128).NotNullable()
            .WithColumn("AlertEventId").AsGuid().Nullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("IsSuccess").AsBoolean().NotNullable()
            .WithColumn("Message").AsString(500).Nullable()
            .WithColumn("SentAt").AsDateTime2().NotNullable();
        Create.Index("IX_NotificationDeliveries_ChannelId_SentAt").OnTable("NotificationDeliveries")
            .OnColumn("ChannelId").Ascending()
            .OnColumn("SentAt").Descending();
        Create.Index("IX_NotificationDeliveries_AlertEventId").OnTable("NotificationDeliveries").OnColumn("AlertEventId");

        Create.Table("UptimeChecks")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_UptimeChecks")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("Type").AsInt32().NotNullable()
            .WithColumn("Url").AsString(500).Nullable()
            .WithColumn("Host").AsString(255).Nullable()
            .WithColumn("Port").AsInt32().Nullable()
            .WithColumn("AcceptedStatusCodes").AsString(100).Nullable()
            .WithColumn("ServerId").AsGuid().Nullable()
                .ForeignKey("FK_UptimeChecks_Servers", "Servers", "Id")
            .WithColumn("IntervalSeconds").AsInt32().NotNullable()
            .WithColumn("TimeoutSeconds").AsInt32().NotNullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("StatusChangedAt").AsDateTime2().Nullable()
            .WithColumn("LastCheckedAt").AsDateTime2().Nullable()
            .WithColumn("LastResponseMs").AsInt32().Nullable()
            .WithColumn("LastStatusCode").AsInt32().Nullable()
            .WithColumn("LastError").AsString(500).Nullable()
            .WithColumn("ConsecutiveFailures").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
        Create.Index("IX_UptimeChecks_ServerId").OnTable("UptimeChecks").OnColumn("ServerId");
        Execute.Sql("CREATE UNIQUE INDEX UX_UptimeChecks_Name ON UptimeChecks (Name) WHERE IsDeleted = 0;");

        Create.Table("UptimeCheckResults")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_UptimeCheckResults").Identity()
            .WithColumn("CheckId").AsGuid().NotNullable()
            .WithColumn("CheckedAt").AsDateTime2().NotNullable()
            .WithColumn("IsUp").AsBoolean().NotNullable()
            .WithColumn("ResponseMs").AsInt32().NotNullable()
            .WithColumn("StatusCode").AsInt32().Nullable()
            .WithColumn("Message").AsString(500).Nullable();
        Create.Index("IX_UptimeCheckResults_CheckId_CheckedAt").OnTable("UptimeCheckResults")
            .OnColumn("CheckId").Ascending()
            .OnColumn("CheckedAt").Descending();
        Create.Index("IX_UptimeCheckResults_CheckedAt").OnTable("UptimeCheckResults").OnColumn("CheckedAt");

        Create.Table("SslCertificateMonitors")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_SslCertificateMonitors")
            .WithColumn("Host").AsString(255).NotNullable()
            .WithColumn("Port").AsInt32().NotNullable()
            .WithColumn("ServerId").AsGuid().Nullable()
                .ForeignKey("FK_SslCertificateMonitors_Servers", "Servers", "Id")
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Subject").AsString(500).Nullable()
            .WithColumn("Issuer").AsString(500).Nullable()
            .WithColumn("NotBefore").AsDateTime2().Nullable()
            .WithColumn("NotAfter").AsDateTime2().Nullable()
            .WithColumn("ResolvedAddress").AsString(64).Nullable()
            .WithColumn("LastCheckedAt").AsDateTime2().Nullable()
            .WithColumn("LastError").AsString(500).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
        Create.Index("IX_SslCertificateMonitors_ServerId").OnTable("SslCertificateMonitors").OnColumn("ServerId");
        Execute.Sql("CREATE UNIQUE INDEX UX_SslCertificateMonitors_Host_Port ON SslCertificateMonitors (Host, Port) WHERE IsDeleted = 0;");
    }

    public override void Down()
    {
        Delete.Table("SslCertificateMonitors");
        Delete.Table("UptimeCheckResults");
        Delete.Table("UptimeChecks");
        Delete.Table("NotificationDeliveries");
        Delete.Table("AlertEvents");
        Delete.Table("AlertRuleChannels");
        Delete.Table("AlertRules");
        Delete.Table("NotificationChannels");
    }
}

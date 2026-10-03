using FluentMigrator;

namespace ServerManager.Plugin.DevOps.Dokploy.Data.Migrations;

[Migration(202610030006, "Dokploy kurulum ve durum tabloları")]
public class M001_DokploySchema : Migration
{
    public override void Up()
    {
        Create.Table("DokployInstances")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_DokployInstances")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_DokployInstances_Servers", "Servers", "Id")
            .WithColumn("BaseUrl").AsString(500).NotNullable()
            .WithColumn("EncryptedApiKey").AsString(int.MaxValue).Nullable()
            .WithColumn("Version").AsString(100).Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("StatusMessage").AsString(1000).Nullable()
            .WithColumn("LastHealthCheckAt").AsDateTime2().Nullable()
            .WithColumn("LastResponseTimeMs").AsInt32().Nullable()
            .WithColumn("InstalledAt").AsDateTime2().Nullable()
            .WithColumn("InstallationId").AsGuid().Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("UX_DokployInstances_ServerId").OnTable("DokployInstances")
            .OnColumn("ServerId").Ascending()
            .WithOptions().Unique();

        Create.Table("DokployInstallations")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_DokployInstallations")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_DokployInstallations_Servers", "Servers", "Id")
            .WithColumn("ServerName").AsString(256).NotNullable()
            .WithColumn("UserId").AsString(64).Nullable()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("IpAddress").AsString(45).Nullable()
            .WithColumn("RequestedVersion").AsString(64).Nullable()
            .WithColumn("ScriptUrl").AsString(500).NotNullable()
            .WithColumn("ScriptSha256").AsString(64).Nullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("ExitCode").AsInt32().Nullable()
            .WithColumn("FailureReason").AsString(1000).Nullable()
            .WithColumn("Output").AsString(int.MaxValue).NotNullable().WithDefaultValue(string.Empty)
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable();

        Create.Index("IX_DokployInstallations_ServerId_StartedAt").OnTable("DokployInstallations")
            .OnColumn("ServerId").Ascending()
            .OnColumn("StartedAt").Descending();
    }

    public override void Down()
    {
        Delete.Table("DokployInstallations");
        Delete.Table("DokployInstances");
    }
}

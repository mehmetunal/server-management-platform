using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030003, "Audit log tablosu")]
public class M003_AuditLogSchema : Migration
{
    public override void Up()
    {
        Create.Table("AuditLogs")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_AuditLogs").Identity()
            .WithColumn("UserId").AsString(64).Nullable()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("Action").AsString(128).NotNullable()
            .WithColumn("EntityType").AsString(64).Nullable()
            .WithColumn("EntityId").AsString(64).Nullable()
            .WithColumn("TargetName").AsString(256).Nullable()
            .WithColumn("Details").AsString(2000).Nullable()
            .WithColumn("IpAddress").AsString(45).Nullable()
            .WithColumn("UserAgent").AsString(512).Nullable()
            .WithColumn("IsSuccess").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime2().NotNullable();

        Create.Index("IX_AuditLogs_CreatedAt").OnTable("AuditLogs").OnColumn("CreatedAt").Descending();
        Create.Index("IX_AuditLogs_Action").OnTable("AuditLogs").OnColumn("Action");
        Create.Index("IX_AuditLogs_UserId").OnTable("AuditLogs").OnColumn("UserId");
    }

    public override void Down()
    {
        Delete.Table("AuditLogs");
    }
}

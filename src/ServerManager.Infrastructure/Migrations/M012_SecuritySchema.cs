using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040012, "Güvenlik taramaları ve audit log bütünlük zinciri")]
public class M012_SecuritySchema : Migration
{
    public override void Up()
    {
        Create.Table("SecurityScans")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_SecurityScans")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_SecurityScans_Servers", "Servers", "Id")
            .WithColumn("ServerName").AsString(128).NotNullable()
            .WithColumn("Trigger").AsInt32().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable()
            .WithColumn("Score").AsInt32().Nullable()
            .WithColumn("CriticalCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("WarningCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsPrivileged").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ReportJson").AsString(int.MaxValue).Nullable()
            .WithColumn("FailureReason").AsString(1000).Nullable()
            .WithColumn("UserName").AsString(256).Nullable();

        Create.Index("IX_SecurityScans_ServerId_StartedAt").OnTable("SecurityScans")
            .OnColumn("ServerId").Ascending()
            .OnColumn("StartedAt").Descending();

        // Eski kayıtlarda boş kalır; zincir bu sürümden sonra yazılan ilk kayıtla başlar.
        Alter.Table("AuditLogs").AddColumn("ChainHash").AsString(64).Nullable();
    }

    public override void Down()
    {
        Delete.Column("ChainHash").FromTable("AuditLogs");
        Delete.Table("SecurityScans");
    }
}

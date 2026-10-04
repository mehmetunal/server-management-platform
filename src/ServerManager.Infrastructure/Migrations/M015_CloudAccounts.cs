using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040015, "Bulut sağlayıcı hesapları ve sunucu bağlantısı")]
public class M015_CloudAccounts : Migration
{
    public override void Up()
    {
        Create.Table("CloudAccounts")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_CloudAccounts")
            .WithColumn("Name").AsString(64).NotNullable()
            .WithColumn("Provider").AsString(64).NotNullable()
            .WithColumn("EncryptedToken").AsString(int.MaxValue).NotNullable()
            .WithColumn("AccountLabel").AsString(200).Nullable()
            .WithColumn("LastSyncAt").AsDateTime2().Nullable()
            .WithColumn("LastSyncError").AsString(1000).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Alter.Table("Servers")
            .AddColumn("CloudAccountId").AsGuid().Nullable()
                .ForeignKey("FK_Servers_CloudAccounts", "CloudAccounts", "Id")
            .AddColumn("CloudExternalId").AsString(128).Nullable();

        Create.Index("IX_Servers_CloudAccountId").OnTable("Servers").OnColumn("CloudAccountId").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_Servers_CloudAccountId").OnTable("Servers");
        Delete.ForeignKey("FK_Servers_CloudAccounts").OnTable("Servers");
        Delete.Column("CloudExternalId").FromTable("Servers");
        Delete.Column("CloudAccountId").FromTable("Servers");
        Delete.Table("CloudAccounts");
    }
}

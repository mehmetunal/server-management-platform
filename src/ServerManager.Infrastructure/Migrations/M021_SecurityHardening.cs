using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610050021, "Data Protection anahtarları ve audit zincir çapası")]
public class M021_SecurityHardening : Migration
{
    public override void Up()
    {
        // Microsoft.AspNetCore.DataProtection.EntityFrameworkCore'un beklediği şema.
        Create.Table("DataProtectionKeys")
            .WithColumn("Id").AsInt32().PrimaryKey("PK_DataProtectionKeys").Identity()
            .WithColumn("FriendlyName").AsString(int.MaxValue).Nullable()
            .WithColumn("Xml").AsString(int.MaxValue).Nullable();

        Create.Table("AuditChainAnchors")
            .WithColumn("Id").AsInt32().PrimaryKey("PK_AuditChainAnchors")
            .WithColumn("FirstSignedId").AsInt64().NotNullable()
            .WithColumn("SigningStartedAt").AsDateTime2().NotNullable()
            .WithColumn("LastId").AsInt64().NotNullable()
            .WithColumn("LastHash").AsString(64).NotNullable()
            .WithColumn("SignedCount").AsInt64().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime2().NotNullable()
            .WithColumn("Signature").AsString(64).NotNullable();
    }

    public override void Down()
    {
        Delete.Table("AuditChainAnchors");
        Delete.Table("DataProtectionKeys");
    }
}

using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030002, "Sunucu, credential ve etiket tabloları")]
public class M002_ServerSchema : Migration
{
    public override void Up()
    {
        Create.Table("Servers")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_Servers")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("Hostname").AsString(255).NotNullable()
            .WithColumn("IpAddress").AsString(45).NotNullable()
            .WithColumn("SshPort").AsInt32().NotNullable().WithDefaultValue(22)
            .WithColumn("Username").AsString(64).NotNullable()
            .WithColumn("AuthenticationType").AsInt32().NotNullable()
            .WithColumn("UseSudo").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Description").AsString(1000).Nullable()
            .WithColumn("Environment").AsInt32().NotNullable()
            .WithColumn("Location").AsString(128).Nullable()
            .WithColumn("Provider").AsString(128).Nullable()
            .WithColumn("OperatingSystem").AsString(128).Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("HostKeyFingerprint").AsString(128).Nullable()
            .WithColumn("LastConnectionTestAt").AsDateTime2().Nullable()
            .WithColumn("LastConnectionSucceeded").AsBoolean().Nullable()
            .WithColumn("LastConnectionMessage").AsString(500).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Execute.Sql("CREATE UNIQUE INDEX [UX_Servers_Name] ON [Servers] ([Name]) WHERE [IsDeleted] = 0;");
        Create.Index("IX_Servers_Status").OnTable("Servers").OnColumn("Status");
        Create.Index("IX_Servers_Environment").OnTable("Servers").OnColumn("Environment");

        Create.Table("ServerCredentials")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ServerCredentials")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ServerCredentials_Servers_ServerId", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("EncryptedPassword").AsString(int.MaxValue).Nullable()
            .WithColumn("EncryptedPrivateKey").AsString(int.MaxValue).Nullable()
            .WithColumn("EncryptedPassphrase").AsString(int.MaxValue).Nullable()
            .WithColumn("EncryptedSudoPassword").AsString(int.MaxValue).Nullable()
            .WithColumn("KeyVersion").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("UX_ServerCredentials_ServerId").OnTable("ServerCredentials").OnColumn("ServerId").Unique();

        Create.Table("ServerTags")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ServerTags")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ServerTags_Servers_ServerId", "Servers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Name").AsString(64).NotNullable();

        Create.Index("UX_ServerTags_ServerId_Name").OnTable("ServerTags")
            .OnColumn("ServerId").Ascending()
            .OnColumn("Name").Ascending()
            .WithOptions().Unique();
        Create.Index("IX_ServerTags_Name").OnTable("ServerTags").OnColumn("Name");
    }

    public override void Down()
    {
        Delete.Table("ServerTags");
        Delete.Table("ServerCredentials");
        Delete.Table("Servers");
    }
}

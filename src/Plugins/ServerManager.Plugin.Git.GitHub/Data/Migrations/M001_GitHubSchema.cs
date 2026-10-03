using FluentMigrator;

namespace ServerManager.Plugin.Git.GitHub.Data.Migrations;

[Migration(202610040002, "GitHub App tablosu")]
public class M001_GitHubSchema : Migration
{
    public override void Up()
    {
        Create.Table("GitHubApps")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_GitHubApps")
            .WithColumn("Name").AsString(256).NotNullable()
            .WithColumn("AppId").AsInt64().NotNullable()
            .WithColumn("Slug").AsString(256).NotNullable()
            .WithColumn("OwnerLogin").AsString(256).Nullable()
            .WithColumn("HtmlUrl").AsString(500).Nullable()
            .WithColumn("ClientId").AsString(100).Nullable()
            .WithColumn("EncryptedClientSecret").AsString(int.MaxValue).Nullable()
            .WithColumn("EncryptedPrivateKey").AsString(int.MaxValue).NotNullable()
            .WithColumn("EncryptedWebhookSecret").AsString(int.MaxValue).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("IX_GitHubApps_AppId").OnTable("GitHubApps")
            .OnColumn("AppId").Ascending();
    }

    public override void Down() =>
        Delete.Table("GitHubApps");
}

using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030007, "Kurulu eklentiler tablosu")]
public class M007_PluginSchema : Migration
{
    public override void Up()
    {
        Create.Table("InstalledPlugins")
            .WithColumn("SystemName").AsString(128).PrimaryKey("PK_InstalledPlugins")
            .WithColumn("Version").AsString(32).NotNullable()
            .WithColumn("IsEnabled").AsBoolean().NotNullable()
            .WithColumn("InstalledAt").AsDateTime2().NotNullable()
            .WithColumn("InstalledBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
    }

    public override void Down()
    {
        Delete.Table("InstalledPlugins");
    }
}

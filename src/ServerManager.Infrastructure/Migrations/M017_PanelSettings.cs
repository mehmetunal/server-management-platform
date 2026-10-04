using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040017, "Panelden değiştirilen çalışma ayarları")]
public class M017_PanelSettings : Migration
{
    public override void Up()
    {
        Create.Table("PanelSettings")
            .WithColumn("Key").AsString(80).NotNullable().PrimaryKey()
            .WithColumn("Value").AsString(64).NotNullable()
            .WithColumn("UpdatedAt").AsDateTime2().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("PanelSettings");
    }
}

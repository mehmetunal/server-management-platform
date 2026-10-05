using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610050025, "Yedekleme: MongoDB kimlik doğrulama veritabanı")]
public class M025_BackupDatabaseAuthSource : Migration
{
    public override void Up()
    {
        Alter.Table("BackupJobs")
            .AddColumn("DatabaseAuthSource").AsString(128).Nullable();
    }

    public override void Down()
    {
        Delete.Column("DatabaseAuthSource").FromTable("BackupJobs");
    }
}

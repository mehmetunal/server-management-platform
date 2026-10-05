using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610050022, "Alarm kapanışı için art arda düzelme sayacı")]
public class M022_AlertRecoveryHysteresis : Migration
{
    public override void Up()
    {
        Alter.Table("AlertEvents")
            .AddColumn("ConsecutiveOkCount").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.DefaultConstraint().OnTable("AlertEvents").OnColumn("ConsecutiveOkCount");
        Delete.Column("ConsecutiveOkCount").FromTable("AlertEvents");
    }
}

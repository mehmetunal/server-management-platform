using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040016, "Sunucu agent token'ı ve son rapor bilgisi")]
public class M016_ServerAgent : Migration
{
    public override void Up()
    {
        Alter.Table("Servers")
            .AddColumn("AgentTokenHash").AsString(64).Nullable()
            .AddColumn("AgentTokenCreatedAt").AsDateTime2().Nullable()
            .AddColumn("AgentLastSeenAt").AsDateTime2().Nullable()
            .AddColumn("AgentVersion").AsString(32).Nullable();

        Execute.Sql("CREATE UNIQUE INDEX [IX_Servers_AgentTokenHash] ON [Servers] ([AgentTokenHash]) WHERE [AgentTokenHash] IS NOT NULL");
    }

    public override void Down()
    {
        Delete.Index("IX_Servers_AgentTokenHash").OnTable("Servers");
        Delete.Column("AgentVersion").FromTable("Servers");
        Delete.Column("AgentLastSeenAt").FromTable("Servers");
        Delete.Column("AgentTokenCreatedAt").FromTable("Servers");
        Delete.Column("AgentTokenHash").FromTable("Servers");
    }
}

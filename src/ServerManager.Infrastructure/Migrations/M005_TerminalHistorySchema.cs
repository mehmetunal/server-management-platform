using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030005, "Terminal oturum ve komut geçmişi tabloları")]
public class M005_TerminalHistorySchema : Migration
{
    public override void Up()
    {
        Create.Table("TerminalSessions")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_TerminalSessions")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_TerminalSessions_Servers", "Servers", "Id")
            .WithColumn("ServerName").AsString(256).NotNullable()
            .WithColumn("UserId").AsString(64).Nullable()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("IpAddress").AsString(45).Nullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("Container").AsString(255).Nullable()
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("EndedAt").AsDateTime2().Nullable()
            .WithColumn("CloseReason").AsString(256).Nullable()
            .WithColumn("CommandCount").AsInt32().NotNullable().WithDefaultValue(0);

        Create.Index("IX_TerminalSessions_ServerId_StartedAt").OnTable("TerminalSessions")
            .OnColumn("ServerId").Ascending()
            .OnColumn("StartedAt").Descending();
        Create.Index("IX_TerminalSessions_UserId").OnTable("TerminalSessions").OnColumn("UserId");

        Create.Table("TerminalCommands")
            .WithColumn("Id").AsInt64().PrimaryKey("PK_TerminalCommands").Identity()
            .WithColumn("SessionId").AsGuid().NotNullable()
                .ForeignKey("FK_TerminalCommands_TerminalSessions", "TerminalSessions", "Id")
            .WithColumn("ExecutedAt").AsDateTime2().NotNullable()
            .WithColumn("CommandText").AsString(2000).NotNullable()
            .WithColumn("IsApproximate").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("MatchedRule").AsString(256).Nullable();

        Create.Index("IX_TerminalCommands_SessionId_ExecutedAt").OnTable("TerminalCommands")
            .OnColumn("SessionId").Ascending()
            .OnColumn("ExecutedAt").Ascending();
    }

    public override void Down()
    {
        Delete.Table("TerminalCommands");
        Delete.Table("TerminalSessions");
    }
}

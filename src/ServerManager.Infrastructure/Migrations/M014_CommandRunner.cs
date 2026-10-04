using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040014, "Toplu komut çalıştırma geçmişi ve sunucu şablonları")]
public class M014_CommandRunner : Migration
{
    public override void Up()
    {
        Create.Table("ServerTemplates")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ServerTemplates")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("Content").AsString(int.MaxValue).NotNullable()
            .WithColumn("RequiresSudo").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Table("CommandRuns")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_CommandRuns")
            .WithColumn("Command").AsString(8000).NotNullable()
            .WithColumn("TemplateId").AsGuid().Nullable()
            .WithColumn("TemplateName").AsString(128).Nullable()
            .WithColumn("UseSudo").AsBoolean().NotNullable()
            .WithColumn("TimeoutSeconds").AsInt32().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("TargetCount").AsInt32().NotNullable()
            .WithColumn("SucceededCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("FailedCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable()
            .WithColumn("UserId").AsString(450).Nullable()
            .WithColumn("UserName").AsString(256).Nullable();

        Create.Index("IX_CommandRuns_StartedAt").OnTable("CommandRuns").OnColumn("StartedAt").Descending();

        Create.Table("CommandRunTargets")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_CommandRunTargets")
            .WithColumn("RunId").AsGuid().NotNullable()
                .ForeignKey("FK_CommandRunTargets_CommandRuns", "CommandRuns", "Id")
            .WithColumn("ServerId").AsGuid().NotNullable()
            .WithColumn("ServerName").AsString(128).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("ExitCode").AsInt32().Nullable()
            .WithColumn("Output").AsString(int.MaxValue).Nullable()
            .WithColumn("OutputTruncated").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ErrorMessage").AsString(1000).Nullable()
            .WithColumn("StartedAt").AsDateTime2().Nullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable()
            .WithColumn("DurationMs").AsInt64().Nullable();

        Create.Index("IX_CommandRunTargets_RunId").OnTable("CommandRunTargets").OnColumn("RunId").Ascending();
    }

    public override void Down()
    {
        Delete.Table("CommandRunTargets");
        Delete.Table("CommandRuns");
        Delete.Table("ServerTemplates");
    }
}

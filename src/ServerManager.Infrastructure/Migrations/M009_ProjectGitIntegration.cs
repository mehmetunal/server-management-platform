using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040001, "Projelerde Git entegrasyonu (GitHub App vb.) alanları")]
public class M009_ProjectGitIntegration : Migration
{
    public override void Up()
    {
        Alter.Table("DeploymentProjects")
            .AddColumn("GitIntegration").AsString(100).Nullable()
            .AddColumn("GitSourceId").AsString(200).Nullable()
            .AddColumn("GitRepository").AsString(200).Nullable();
    }

    public override void Down()
    {
        Delete.Column("GitIntegration").Column("GitSourceId").Column("GitRepository").FromTable("DeploymentProjects");
    }
}

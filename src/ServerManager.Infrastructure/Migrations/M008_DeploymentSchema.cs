using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030008, "Deployment projeleri ve deployment geçmişi tabloları")]
public class M008_DeploymentSchema : Migration
{
    public override void Up()
    {
        Create.Table("DeploymentProjects")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_DeploymentProjects")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_DeploymentProjects_Servers", "Servers", "Id")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("Slug").AsString(64).NotNullable()
            .WithColumn("Description").AsString(1000).Nullable()
            .WithColumn("GitProvider").AsInt32().NotNullable()
            .WithColumn("RepositoryUrl").AsString(500).NotNullable()
            .WithColumn("Branch").AsString(200).NotNullable()
            .WithColumn("GitUsername").AsString(128).Nullable()
            .WithColumn("EncryptedAccessToken").AsString(int.MaxValue).Nullable()
            .WithColumn("DeployPath").AsString(500).NotNullable()
            .WithColumn("BuildType").AsInt32().NotNullable()
            .WithColumn("ComposeFile").AsString(255).Nullable()
            .WithColumn("DockerfilePath").AsString(255).Nullable()
            .WithColumn("PortMappings").AsString(500).Nullable()
            .WithColumn("BuildCommand").AsString(4000).Nullable()
            .WithColumn("DeployCommand").AsString(4000).Nullable()
            .WithColumn("UseSudoForCommands").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("EncryptedEnvironment").AsString(int.MaxValue).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("IX_DeploymentProjects_ServerId").OnTable("DeploymentProjects").OnColumn("ServerId");
        Execute.Sql("CREATE UNIQUE INDEX UX_DeploymentProjects_Name ON DeploymentProjects (Name) WHERE IsDeleted = 0;");
        Execute.Sql("CREATE UNIQUE INDEX UX_DeploymentProjects_Slug ON DeploymentProjects (Slug);");

        Create.Table("Deployments")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_Deployments")
            .WithColumn("ProjectId").AsGuid().NotNullable()
                .ForeignKey("FK_Deployments_DeploymentProjects", "DeploymentProjects", "Id")
            .WithColumn("ProjectName").AsString(128).NotNullable()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_Deployments_Servers", "Servers", "Id")
            .WithColumn("ServerName").AsString(256).NotNullable()
            .WithColumn("BuildType").AsInt32().NotNullable()
            .WithColumn("Branch").AsString(200).NotNullable()
            .WithColumn("RequestedCommit").AsString(64).Nullable()
            .WithColumn("CommitSha").AsString(64).Nullable()
            .WithColumn("CommitMessage").AsString(500).Nullable()
            .WithColumn("CommitAuthor").AsString(256).Nullable()
            .WithColumn("SourceDeploymentId").AsGuid().Nullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("FailureReason").AsString(1000).Nullable()
            .WithColumn("ExitCode").AsInt32().Nullable()
            .WithColumn("Log").AsString(int.MaxValue).NotNullable().WithDefaultValue(string.Empty)
            .WithColumn("UserId").AsString(64).Nullable()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("IpAddress").AsString(45).Nullable()
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("BuildStartedAt").AsDateTime2().Nullable()
            .WithColumn("DeployStartedAt").AsDateTime2().Nullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable()
            .WithColumn("CancelledBy").AsString(256).Nullable();

        Create.Index("IX_Deployments_ProjectId_StartedAt").OnTable("Deployments")
            .OnColumn("ProjectId").Ascending()
            .OnColumn("StartedAt").Descending();
        Create.Index("IX_Deployments_ServerId_StartedAt").OnTable("Deployments")
            .OnColumn("ServerId").Ascending()
            .OnColumn("StartedAt").Descending();
        Create.Index("IX_Deployments_Status").OnTable("Deployments").OnColumn("Status");
    }

    public override void Down()
    {
        Delete.Table("Deployments");
        Delete.Table("DeploymentProjects");
    }
}

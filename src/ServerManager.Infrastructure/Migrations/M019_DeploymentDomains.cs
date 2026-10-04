using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040019, "Deployment domain kayıtları")]
public class M019_DeploymentDomains : Migration
{
    public override void Up()
    {
        Create.Table("DeploymentDomains")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_DeploymentDomains")
            .WithColumn("ProjectId").AsGuid().NotNullable()
                .ForeignKey("FK_DeploymentDomains_DeploymentProjects", "DeploymentProjects", "Id")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_DeploymentDomains_Servers", "Servers", "Id")
            .WithColumn("Host").AsString(253).NotNullable()
            .WithColumn("Path").AsString(200).NotNullable().WithDefaultValue(string.Empty)
            .WithColumn("ContainerPort").AsInt32().NotNullable()
            .WithColumn("ServiceName").AsString(63).Nullable()
            .WithColumn("TlsMode").AsInt32().NotNullable()
            .WithColumn("EncryptedCertificate").AsString(int.MaxValue).Nullable()
            .WithColumn("EncryptedPrivateKey").AsString(int.MaxValue).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("IX_DeploymentDomains_ProjectId").OnTable("DeploymentDomains").OnColumn("ProjectId");
        Execute.Sql("CREATE UNIQUE INDEX UX_DeploymentDomains_Server_Host_Path ON DeploymentDomains (ServerId, Host, Path) WHERE IsDeleted = 0;");
    }

    public override void Down()
    {
        Delete.Table("DeploymentDomains");
    }
}

using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610050026, "Servisler: deployment projesine bağlı servisler")]
public class M026_ProjectServiceLinks : Migration
{
    public override void Up()
    {
        Create.Table("ProjectServiceLinks")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ProjectServiceLinks")
            .WithColumn("ProjectId").AsGuid().NotNullable()
                .ForeignKey("FK_ProjectServiceLinks_DeploymentProjects", "DeploymentProjects", "Id")
            .WithColumn("ManagedServiceId").AsGuid().NotNullable()
                .ForeignKey("FK_ProjectServiceLinks_ManagedServices", "ManagedServices", "Id")
            .WithColumn("EnvironmentKeys").AsString(2000).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("UX_ProjectServiceLinks_ProjectId_ManagedServiceId").OnTable("ProjectServiceLinks")
            .OnColumn("ProjectId").Ascending()
            .OnColumn("ManagedServiceId").Ascending()
            .WithOptions().Unique();
        Create.Index("IX_ProjectServiceLinks_ManagedServiceId").OnTable("ProjectServiceLinks").OnColumn("ManagedServiceId");
    }

    public override void Down()
    {
        Delete.Table("ProjectServiceLinks");
    }
}

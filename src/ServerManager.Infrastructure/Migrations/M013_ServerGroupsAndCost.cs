using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040013, "Sunucu grupları ve aylık maliyet alanları")]
public class M013_ServerGroupsAndCost : Migration
{
    public override void Up()
    {
        Create.Table("ServerGroups")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ServerGroups")
            .WithColumn("Name").AsString(64).NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("Color").AsString(16).NotNullable().WithDefaultValue("slate")
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("IX_ServerGroups_Name").OnTable("ServerGroups").OnColumn("Name").Ascending();

        Alter.Table("Servers")
            .AddColumn("GroupId").AsGuid().Nullable()
                .ForeignKey("FK_Servers_ServerGroups", "ServerGroups", "Id")
            .AddColumn("MonthlyCost").AsDecimal(12, 2).Nullable()
            .AddColumn("CostCurrency").AsString(3).Nullable();

        Create.Index("IX_Servers_GroupId").OnTable("Servers").OnColumn("GroupId").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_Servers_GroupId").OnTable("Servers");
        Delete.ForeignKey("FK_Servers_ServerGroups").OnTable("Servers");
        Delete.Column("GroupId").Column("MonthlyCost").Column("CostCurrency").FromTable("Servers");
        Delete.Table("ServerGroups");
    }
}

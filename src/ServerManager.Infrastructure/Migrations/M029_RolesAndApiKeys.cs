using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610060029, "Rol yönetimi (seed edilmiş izinlerin takibi) ve kişisel API anahtarları")]
public class M029_RolesAndApiKeys : Migration
{
    public override void Up()
    {
        Create.Table("RoleKnownPermissions")
            .WithColumn("RoleId").AsGuid().NotNullable().PrimaryKey("PK_RoleKnownPermissions")
                .ForeignKey("FK_RoleKnownPermissions_AspNetRoles_RoleId", "AspNetRoles", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Permission").AsString(256).NotNullable().PrimaryKey("PK_RoleKnownPermissions");

        // Yükseltmede: rollerde şu an bulunan izinler "bilinen" sayılır. Rolde olmayan varsayılan izinler (ör. bu sürümde
        // gelen roles.manage) ilk açılışta bir kez eklenir; sonra yönetici kaldırırsa geri gelmez.
        Execute.Sql("""
            INSERT INTO [RoleKnownPermissions] ([RoleId], [Permission])
            SELECT DISTINCT [RoleId], LEFT([ClaimValue], 256)
            FROM [AspNetRoleClaims]
            WHERE [ClaimType] = 'permission' AND [ClaimValue] IS NOT NULL;
            """);

        Create.Table("ApiKeys")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ApiKeys")
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_ApiKeys_AspNetUsers_UserId", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Prefix").AsString(32).NotNullable()
            .WithColumn("KeyHash").AsString(64).NotNullable()
            .WithColumn("Scopes").AsString(4000).NotNullable()
            .WithColumn("AllowedIps").AsString(1000).Nullable()
            .WithColumn("ExpiresAt").AsDateTime2().Nullable()
            .WithColumn("LastUsedAt").AsDateTime2().Nullable()
            .WithColumn("LastUsedIp").AsString(64).Nullable()
            .WithColumn("RevokedAt").AsDateTime2().Nullable()
            .WithColumn("RevokedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        Create.Index("UX_ApiKeys_Prefix").OnTable("ApiKeys").OnColumn("Prefix").Ascending().WithOptions().Unique();
        Create.Index("IX_ApiKeys_UserId").OnTable("ApiKeys").OnColumn("UserId");
    }

    public override void Down()
    {
        Delete.Table("ApiKeys");
        Delete.Table("RoleKnownPermissions");
    }
}

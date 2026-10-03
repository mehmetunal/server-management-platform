using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610030001, "ASP.NET Core Identity şeması")]
public class M001_IdentitySchema : Migration
{
    public override void Up()
    {
        Create.Table("AspNetRoles")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_AspNetRoles")
            .WithColumn("Name").AsString(256).Nullable()
            .WithColumn("NormalizedName").AsString(256).Nullable()
            .WithColumn("ConcurrencyStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("Description").AsString(256).Nullable();

        Execute.Sql("CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;");

        Create.Table("AspNetUsers")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_AspNetUsers")
            .WithColumn("FullName").AsString(128).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("LastLoginAt").AsDateTime2().Nullable()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("NormalizedUserName").AsString(256).Nullable()
            .WithColumn("Email").AsString(256).Nullable()
            .WithColumn("NormalizedEmail").AsString(256).Nullable()
            .WithColumn("EmailConfirmed").AsBoolean().NotNullable()
            .WithColumn("PasswordHash").AsString(int.MaxValue).Nullable()
            .WithColumn("SecurityStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("ConcurrencyStamp").AsString(int.MaxValue).Nullable()
            .WithColumn("PhoneNumber").AsString(int.MaxValue).Nullable()
            .WithColumn("PhoneNumberConfirmed").AsBoolean().NotNullable()
            .WithColumn("TwoFactorEnabled").AsBoolean().NotNullable()
            .WithColumn("LockoutEnd").AsDateTimeOffset().Nullable()
            .WithColumn("LockoutEnabled").AsBoolean().NotNullable()
            .WithColumn("AccessFailedCount").AsInt32().NotNullable();

        Execute.Sql("CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;");
        Create.Index("EmailIndex").OnTable("AspNetUsers").OnColumn("NormalizedEmail");

        Create.Table("AspNetRoleClaims")
            .WithColumn("Id").AsInt32().PrimaryKey("PK_AspNetRoleClaims").Identity()
            .WithColumn("RoleId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetRoleClaims_AspNetRoles_RoleId", "AspNetRoles", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ClaimType").AsString(int.MaxValue).Nullable()
            .WithColumn("ClaimValue").AsString(int.MaxValue).Nullable();

        Create.Index("IX_AspNetRoleClaims_RoleId").OnTable("AspNetRoleClaims").OnColumn("RoleId");

        Create.Table("AspNetUserClaims")
            .WithColumn("Id").AsInt32().PrimaryKey("PK_AspNetUserClaims").Identity()
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserClaims_AspNetUsers_UserId", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ClaimType").AsString(int.MaxValue).Nullable()
            .WithColumn("ClaimValue").AsString(int.MaxValue).Nullable();

        Create.Index("IX_AspNetUserClaims_UserId").OnTable("AspNetUserClaims").OnColumn("UserId");

        Create.Table("AspNetUserLogins")
            .WithColumn("LoginProvider").AsString(128).NotNullable().PrimaryKey("PK_AspNetUserLogins")
            .WithColumn("ProviderKey").AsString(128).NotNullable().PrimaryKey("PK_AspNetUserLogins")
            .WithColumn("ProviderDisplayName").AsString(int.MaxValue).Nullable()
            .WithColumn("UserId").AsGuid().NotNullable()
                .ForeignKey("FK_AspNetUserLogins_AspNetUsers_UserId", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_AspNetUserLogins_UserId").OnTable("AspNetUserLogins").OnColumn("UserId");

        Create.Table("AspNetUserRoles")
            .WithColumn("UserId").AsGuid().NotNullable().PrimaryKey("PK_AspNetUserRoles")
                .ForeignKey("FK_AspNetUserRoles_AspNetUsers_UserId", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("RoleId").AsGuid().NotNullable().PrimaryKey("PK_AspNetUserRoles")
                .ForeignKey("FK_AspNetUserRoles_AspNetRoles_RoleId", "AspNetRoles", "Id").OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_AspNetUserRoles_RoleId").OnTable("AspNetUserRoles").OnColumn("RoleId");

        Create.Table("AspNetUserTokens")
            .WithColumn("UserId").AsGuid().NotNullable().PrimaryKey("PK_AspNetUserTokens")
                .ForeignKey("FK_AspNetUserTokens_AspNetUsers_UserId", "AspNetUsers", "Id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("LoginProvider").AsString(128).NotNullable().PrimaryKey("PK_AspNetUserTokens")
            .WithColumn("Name").AsString(128).NotNullable().PrimaryKey("PK_AspNetUserTokens")
            .WithColumn("Value").AsString(int.MaxValue).Nullable();
    }

    public override void Down()
    {
        Delete.Table("AspNetUserTokens");
        Delete.Table("AspNetUserRoles");
        Delete.Table("AspNetUserLogins");
        Delete.Table("AspNetUserClaims");
        Delete.Table("AspNetRoleClaims");
        Delete.Table("AspNetUsers");
        Delete.Table("AspNetRoles");
    }
}

using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610050023, "Servisler: tek tıkla kurulan Docker servisleri ve işlem geçmişi")]
public class M023_ManagedServices : Migration
{
    public override void Up()
    {
        Create.Table("ManagedServices")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ManagedServices")
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_ManagedServices_Servers", "Servers", "Id")
            .WithColumn("Name").AsString(64).NotNullable()
            .WithColumn("Slug").AsString(48).NotNullable()
            .WithColumn("TemplateKey").AsString(32).NotNullable()
            .WithColumn("ImageTag").AsString(128).NotNullable()
            .WithColumn("ContainerName").AsString(64).NotNullable()
            .WithColumn("EncryptedCredentials").AsString(int.MaxValue).NotNullable()
            .WithColumn("EncryptedEnvironment").AsString(int.MaxValue).Nullable()
            .WithColumn("PortBindings").AsString(1000).NotNullable().WithDefaultValue("[]")
            .WithColumn("ExposePublicly").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("AllowedSourceIps").AsString(4000).Nullable()
            .WithColumn("VolumeMode").AsInt32().NotNullable()
            .WithColumn("HostDataPath").AsString(500).Nullable()
            .WithColumn("MemoryLimitMb").AsInt32().Nullable()
            .WithColumn("CpuLimit").AsDecimal(6, 2).Nullable()
            .WithColumn("Networks").AsString(500).Nullable()
            .WithColumn("JoinProxyNetwork").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("LastError").AsString(1000).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();

        // Kısa ad (container, volume ve güvenlik duvarı etiketi) sunucuda bir kez kullanılır; silinen servisin volume'u
        // korunmuş olabileceği için silinmiş kayıtlar da sayılır. Ad yalnızca silinmemiş servisler arasında benzersizdir.
        Execute.Sql("CREATE UNIQUE INDEX UX_ManagedServices_ServerId_Slug ON ManagedServices (ServerId, Slug);");
        Execute.Sql("CREATE UNIQUE INDEX UX_ManagedServices_ServerId_Name ON ManagedServices (ServerId, Name) WHERE IsDeleted = 0;");

        Create.Table("ManagedServiceOperations")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_ManagedServiceOperations")
            .WithColumn("ServiceId").AsGuid().NotNullable()
                .ForeignKey("FK_ManagedServiceOperations_ManagedServices", "ManagedServices", "Id")
            .WithColumn("ServerId").AsGuid().NotNullable()
            .WithColumn("ServiceName").AsString(64).NotNullable()
            .WithColumn("Kind").AsInt32().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("Stage").AsString(32).Nullable()
            .WithColumn("FromTag").AsString(128).Nullable()
            .WithColumn("ToTag").AsString(128).Nullable()
            .WithColumn("RemoveData").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("FailureReason").AsString(1000).Nullable()
            .WithColumn("Log").AsString(int.MaxValue).NotNullable().WithDefaultValue(string.Empty)
            .WithColumn("UserId").AsString(64).Nullable()
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("IpAddress").AsString(45).Nullable()
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("FinishedAt").AsDateTime2().Nullable();

        Create.Index("IX_ManagedServiceOperations_ServiceId_StartedAt").OnTable("ManagedServiceOperations")
            .OnColumn("ServiceId").Ascending()
            .OnColumn("StartedAt").Descending();
        Create.Index("IX_ManagedServiceOperations_Status").OnTable("ManagedServiceOperations").OnColumn("Status");
    }

    public override void Down()
    {
        Delete.Table("ManagedServiceOperations");
        Delete.Table("ManagedServices");
    }
}

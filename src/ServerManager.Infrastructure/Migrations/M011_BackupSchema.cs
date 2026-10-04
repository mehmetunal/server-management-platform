using FluentMigrator;

namespace ServerManager.Infrastructure.Migrations;

[Migration(202610040011, "Yedekleme depolama hedefleri, yedekleme işleri ve çalışma geçmişi")]
public class M011_BackupSchema : Migration
{
    public override void Up()
    {
        Create.Table("BackupStorages")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_BackupStorages")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("ProviderSystemName").AsString(100).NotNullable()
            .WithColumn("EncryptedSettings").AsString(int.MaxValue).NotNullable()
            .WithColumn("LastTestedAt").AsDateTime2().Nullable()
            .WithColumn("LastError").AsString(500).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
        Execute.Sql("CREATE UNIQUE INDEX UX_BackupStorages_Name ON BackupStorages (Name) WHERE IsDeleted = 0;");

        Create.Table("BackupJobs")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_BackupJobs")
            .WithColumn("Name").AsString(128).NotNullable()
            .WithColumn("ServerId").AsGuid().NotNullable()
                .ForeignKey("FK_BackupJobs_Servers", "Servers", "Id")
            .WithColumn("StorageId").AsGuid().NotNullable()
                .ForeignKey("FK_BackupJobs_BackupStorages", "BackupStorages", "Id")
            .WithColumn("SourceType").AsInt32().NotNullable()
            .WithColumn("Paths").AsString(4000).Nullable()
            .WithColumn("Excludes").AsString(2000).Nullable()
            .WithColumn("VolumeName").AsString(255).Nullable()
            .WithColumn("DatabaseEngine").AsInt32().Nullable()
            .WithColumn("ContainerName").AsString(255).Nullable()
            .WithColumn("DatabaseName").AsString(128).Nullable()
            .WithColumn("DatabaseUser").AsString(128).Nullable()
            .WithColumn("EncryptedDatabasePassword").AsString(int.MaxValue).Nullable()
            .WithColumn("DatabaseHost").AsString(255).Nullable()
            .WithColumn("DatabasePort").AsInt32().Nullable()
            .WithColumn("EncryptionEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("EncryptedPassphrase").AsString(int.MaxValue).Nullable()
            .WithColumn("ScheduleType").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ScheduleIntervalHours").AsInt32().NotNullable().WithDefaultValue(24)
            .WithColumn("ScheduleMinuteOfDay").AsInt32().NotNullable().WithDefaultValue(180)
            .WithColumn("ScheduleDayOfWeek").AsInt32().Nullable()
            .WithColumn("NextRunAt").AsDateTime2().Nullable()
            .WithColumn("KeepLast").AsInt32().NotNullable().WithDefaultValue(7)
            .WithColumn("KeepDays").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("IsEnabled").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("LastRunAt").AsDateTime2().Nullable()
            .WithColumn("LastRunStatus").AsInt32().Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTime2().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedAt").AsDateTime2().Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable();
        Create.Index("IX_BackupJobs_ServerId").OnTable("BackupJobs").OnColumn("ServerId");
        Create.Index("IX_BackupJobs_StorageId").OnTable("BackupJobs").OnColumn("StorageId");
        Create.Index("IX_BackupJobs_NextRunAt").OnTable("BackupJobs").OnColumn("NextRunAt");
        Execute.Sql("CREATE UNIQUE INDEX UX_BackupJobs_Name ON BackupJobs (Name) WHERE IsDeleted = 0;");

        Create.Table("BackupRuns")
            .WithColumn("Id").AsGuid().PrimaryKey("PK_BackupRuns")
            .WithColumn("Operation").AsInt32().NotNullable()
            .WithColumn("Trigger").AsInt32().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("JobId").AsGuid().Nullable()
            .WithColumn("JobName").AsString(128).NotNullable()
            .WithColumn("SourceType").AsInt32().NotNullable()
            .WithColumn("ServerId").AsGuid().NotNullable()
            .WithColumn("ServerName").AsString(128).NotNullable()
            .WithColumn("StorageId").AsGuid().Nullable()
            .WithColumn("StorageName").AsString(128).NotNullable()
            .WithColumn("ObjectKey").AsString(1024).Nullable()
            .WithColumn("FileName").AsString(255).Nullable()
            .WithColumn("SizeBytes").AsInt64().Nullable()
            .WithColumn("Sha256").AsString(64).Nullable()
            .WithColumn("IsEncrypted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("EncryptedPassphrase").AsString(int.MaxValue).Nullable()
            .WithColumn("SourceRunId").AsGuid().Nullable()
            .WithColumn("RestoreTarget").AsString(500).Nullable()
            .WithColumn("FailureReason").AsString(1000).Nullable()
            .WithColumn("Log").AsString(int.MaxValue).NotNullable().WithDefaultValue(string.Empty)
            .WithColumn("UserName").AsString(256).Nullable()
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable()
            .WithColumn("CancelledBy").AsString(256).Nullable()
            .WithColumn("ArtifactDeletedAt").AsDateTime2().Nullable()
            .WithColumn("ArtifactDeletedBy").AsString(256).Nullable();
        Create.Index("IX_BackupRuns_JobId_StartedAt").OnTable("BackupRuns")
            .OnColumn("JobId").Ascending()
            .OnColumn("StartedAt").Descending();
        Create.Index("IX_BackupRuns_StartedAt").OnTable("BackupRuns").OnColumn("StartedAt");
        Create.Index("IX_BackupRuns_Status").OnTable("BackupRuns").OnColumn("Status");
    }

    public override void Down()
    {
        Delete.Table("BackupRuns");
        Delete.Table("BackupJobs");
        Delete.Table("BackupStorages");
    }
}

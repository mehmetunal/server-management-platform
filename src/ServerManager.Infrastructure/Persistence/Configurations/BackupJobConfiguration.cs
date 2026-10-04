using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> builder)
    {
        builder.ToTable("BackupJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.Name).HasMaxLength(128).IsRequired();
        builder.Property(j => j.SourceType).HasConversion<int>();
        builder.Property(j => j.Paths).HasMaxLength(4000);
        builder.Property(j => j.Excludes).HasMaxLength(2000);
        builder.Property(j => j.VolumeName).HasMaxLength(255);
        builder.Property(j => j.DatabaseEngine).HasConversion<int?>();
        builder.Property(j => j.ContainerName).HasMaxLength(255);
        builder.Property(j => j.DatabaseName).HasMaxLength(128);
        builder.Property(j => j.DatabaseUser).HasMaxLength(128);
        builder.Property(j => j.DatabaseHost).HasMaxLength(255);
        builder.Property(j => j.ScheduleType).HasConversion<int>();
        builder.Property(j => j.ScheduleDayOfWeek).HasConversion<int?>();
        builder.Property(j => j.LastRunStatus).HasConversion<int?>();
        builder.Property(j => j.DeletedBy).HasMaxLength(256);
        builder.Property(j => j.CreatedBy).HasMaxLength(256);
        builder.Property(j => j.UpdatedBy).HasMaxLength(256);

        builder.HasOne(j => j.Server)
            .WithMany()
            .HasForeignKey(j => j.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(j => j.Storage)
            .WithMany()
            .HasForeignKey(j => j.StorageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sunucusu silinen iş de gizlenir; zorunlu ilişkide filtreler tutarlı olmalıdır.
        builder.HasQueryFilter(j => !j.IsDeleted && !j.Server!.IsDeleted && !j.Storage!.IsDeleted);
    }
}

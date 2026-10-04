using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class BackupRunConfiguration : IEntityTypeConfiguration<BackupRun>
{
    public void Configure(EntityTypeBuilder<BackupRun> builder)
    {
        builder.ToTable("BackupRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Operation).HasConversion<int>();
        builder.Property(r => r.Trigger).HasConversion<int>();
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.SourceType).HasConversion<int>();
        builder.Property(r => r.JobName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.ServerName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.StorageName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.ObjectKey).HasMaxLength(1024);
        builder.Property(r => r.FileName).HasMaxLength(255);
        builder.Property(r => r.Sha256).HasMaxLength(64);
        builder.Property(r => r.RestoreTarget).HasMaxLength(500);
        builder.Property(r => r.FailureReason).HasMaxLength(1000);
        builder.Property(r => r.Log).IsRequired();
        builder.Property(r => r.UserName).HasMaxLength(256);
        builder.Property(r => r.CancelledBy).HasMaxLength(256);
        builder.Property(r => r.ArtifactDeletedBy).HasMaxLength(256);
    }
}

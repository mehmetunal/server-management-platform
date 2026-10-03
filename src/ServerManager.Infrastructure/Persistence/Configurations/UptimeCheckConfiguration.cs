using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class UptimeCheckConfiguration : IEntityTypeConfiguration<UptimeCheck>
{
    public void Configure(EntityTypeBuilder<UptimeCheck> builder)
    {
        builder.ToTable("UptimeChecks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(128).IsRequired();
        builder.Property(c => c.Type).HasConversion<int>();
        builder.Property(c => c.Url).HasMaxLength(500);
        builder.Property(c => c.Host).HasMaxLength(255);
        builder.Property(c => c.AcceptedStatusCodes).HasMaxLength(100);
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.LastError).HasMaxLength(500);
        builder.Property(c => c.DeletedBy).HasMaxLength(256);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasOne(c => c.Server)
            .WithMany()
            .HasForeignKey(c => c.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

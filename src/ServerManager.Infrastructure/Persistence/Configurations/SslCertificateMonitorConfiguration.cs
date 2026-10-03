using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class SslCertificateMonitorConfiguration : IEntityTypeConfiguration<SslCertificateMonitor>
{
    public void Configure(EntityTypeBuilder<SslCertificateMonitor> builder)
    {
        builder.ToTable("SslCertificateMonitors");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Host).HasMaxLength(255).IsRequired();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.Subject).HasMaxLength(500);
        builder.Property(m => m.Issuer).HasMaxLength(500);
        builder.Property(m => m.ResolvedAddress).HasMaxLength(64);
        builder.Property(m => m.LastError).HasMaxLength(500);
        builder.Property(m => m.DeletedBy).HasMaxLength(256);
        builder.Property(m => m.CreatedBy).HasMaxLength(256);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);

        builder.HasOne(m => m.Server)
            .WithMany()
            .HasForeignKey(m => m.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class SecurityScanConfiguration : IEntityTypeConfiguration<SecurityScan>
{
    public void Configure(EntityTypeBuilder<SecurityScan> builder)
    {
        builder.ToTable("SecurityScans");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.ServerName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Trigger).HasConversion<int>();
        builder.Property(s => s.Status).HasConversion<int>();
        builder.Property(s => s.FailureReason).HasMaxLength(1000);
        builder.Property(s => s.UserName).HasMaxLength(256);
    }
}

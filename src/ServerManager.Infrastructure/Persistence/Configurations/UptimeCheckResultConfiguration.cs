using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class UptimeCheckResultConfiguration : IEntityTypeConfiguration<UptimeCheckResult>
{
    public void Configure(EntityTypeBuilder<UptimeCheckResult> builder)
    {
        builder.ToTable("UptimeCheckResults");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Property(r => r.Message).HasMaxLength(500);
        builder.HasIndex(r => new { r.CheckId, r.CheckedAt });
    }
}

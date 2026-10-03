using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class AlertEventConfiguration : IEntityTypeConfiguration<AlertEvent>
{
    public void Configure(EntityTypeBuilder<AlertEvent> builder)
    {
        builder.ToTable("AlertEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.RuleName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Severity).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ServerName).HasMaxLength(256);
        builder.Property(e => e.TargetKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ResolvedMessage).HasMaxLength(1000);
        builder.Property(e => e.AcknowledgedBy).HasMaxLength(256);

        builder.HasIndex(e => new { e.Status, e.StartedAt });
        builder.HasIndex(e => new { e.RuleId, e.Status });
    }
}

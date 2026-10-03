using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityColumn();

        builder.Property(a => a.UserId).HasMaxLength(64);
        builder.Property(a => a.UserName).HasMaxLength(256);
        builder.Property(a => a.Action).HasMaxLength(128).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(64);
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.TargetName).HasMaxLength(256);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(512);
    }
}

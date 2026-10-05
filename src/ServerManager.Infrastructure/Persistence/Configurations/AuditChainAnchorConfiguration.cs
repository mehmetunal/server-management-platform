using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class AuditChainAnchorConfiguration : IEntityTypeConfiguration<AuditChainAnchor>
{
    public void Configure(EntityTypeBuilder<AuditChainAnchor> builder)
    {
        builder.ToTable("AuditChainAnchors");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.LastHash).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Signature).HasMaxLength(64).IsRequired();
    }
}

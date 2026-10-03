using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class InstalledPluginConfiguration : IEntityTypeConfiguration<InstalledPlugin>
{
    public void Configure(EntityTypeBuilder<InstalledPlugin> builder)
    {
        builder.ToTable("InstalledPlugins");
        builder.HasKey(p => p.SystemName);
        builder.Property(p => p.SystemName).HasMaxLength(128);
        builder.Property(p => p.Version).HasMaxLength(32).IsRequired();
        builder.Property(p => p.InstalledBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);
    }
}

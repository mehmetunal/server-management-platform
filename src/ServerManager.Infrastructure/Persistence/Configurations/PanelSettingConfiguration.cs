using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class PanelSettingConfiguration : IEntityTypeConfiguration<PanelSetting>
{
    public void Configure(EntityTypeBuilder<PanelSetting> builder)
    {
        builder.ToTable("PanelSettings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(80);
        builder.Property(s => s.Value).HasMaxLength(64).IsRequired();
    }
}

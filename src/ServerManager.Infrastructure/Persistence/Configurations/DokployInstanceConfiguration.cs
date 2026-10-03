using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class DokployInstanceConfiguration : IEntityTypeConfiguration<DokployInstance>
{
    public void Configure(EntityTypeBuilder<DokployInstance> builder)
    {
        builder.ToTable("DokployInstances");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.HasIndex(i => i.ServerId).IsUnique();

        builder.Property(i => i.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Version).HasMaxLength(100);
        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.StatusMessage).HasMaxLength(1000);
        builder.Property(i => i.CreatedBy).HasMaxLength(256);
        builder.Property(i => i.UpdatedBy).HasMaxLength(256);
    }
}

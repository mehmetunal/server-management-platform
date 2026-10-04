using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerGroupConfiguration : IEntityTypeConfiguration<ServerGroup>
{
    public void Configure(EntityTypeBuilder<ServerGroup> builder)
    {
        builder.ToTable("ServerGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Name).HasMaxLength(64).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.Color).HasMaxLength(16).IsRequired();
        builder.Property(g => g.DeletedBy).HasMaxLength(256);
        builder.Property(g => g.CreatedBy).HasMaxLength(256);
        builder.Property(g => g.UpdatedBy).HasMaxLength(256);

        builder.HasMany(g => g.Servers)
            .WithOne(s => s.Group)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerTagConfiguration : IEntityTypeConfiguration<ServerTag>
{
    public void Configure(EntityTypeBuilder<ServerTag> builder)
    {
        builder.ToTable("ServerTags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => new { t.ServerId, t.Name }).IsUnique();

        builder.HasQueryFilter(t => !t.Server!.IsDeleted);
    }
}

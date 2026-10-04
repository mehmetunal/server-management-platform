using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerTemplateConfiguration : IEntityTypeConfiguration<ServerTemplate>
{
    public void Configure(EntityTypeBuilder<ServerTemplate> builder)
    {
        builder.ToTable("ServerTemplates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Kind).HasConversion<int>();
        builder.Property(t => t.Content).IsRequired();
        builder.Property(t => t.DeletedBy).HasMaxLength(256);
        builder.Property(t => t.CreatedBy).HasMaxLength(256);
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

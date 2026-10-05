using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ProjectServiceLinkConfiguration : IEntityTypeConfiguration<ProjectServiceLink>
{
    public void Configure(EntityTypeBuilder<ProjectServiceLink> builder)
    {
        builder.ToTable("ProjectServiceLinks");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.EnvironmentKeys).HasMaxLength(2000);
        builder.Property(l => l.CreatedBy).HasMaxLength(256);
        builder.Property(l => l.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(l => new { l.ProjectId, l.ManagedServiceId }).IsUnique();

        builder.HasOne(l => l.Project)
            .WithMany()
            .HasForeignKey(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ManagedService)
            .WithMany()
            .HasForeignKey(l => l.ManagedServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Silinmiş proje veya kaldırılmış servise ait bağlar görünmez (ve projeyi sm-services ağına almaz).
        builder.HasQueryFilter(l => !l.Project!.IsDeleted && !l.ManagedService!.IsDeleted);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class DeploymentProjectConfiguration : IEntityTypeConfiguration<DeploymentProject>
{
    public void Configure(EntityTypeBuilder<DeploymentProject> builder)
    {
        builder.ToTable("DeploymentProjects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(128).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.GitProvider).HasConversion<int>();
        builder.Property(p => p.RepositoryUrl).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Branch).HasMaxLength(200).IsRequired();
        builder.Property(p => p.GitUsername).HasMaxLength(128);
        builder.Property(p => p.DeployPath).HasMaxLength(500).IsRequired();
        builder.Property(p => p.BuildType).HasConversion<int>();
        builder.Property(p => p.ComposeFile).HasMaxLength(255);
        builder.Property(p => p.DockerfilePath).HasMaxLength(255);
        builder.Property(p => p.PortMappings).HasMaxLength(500);
        builder.Property(p => p.BuildCommand).HasMaxLength(4000);
        builder.Property(p => p.DeployCommand).HasMaxLength(4000);
        builder.Property(p => p.DeletedBy).HasMaxLength(256);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        builder.HasOne(p => p.Server)
            .WithMany()
            .HasForeignKey(p => p.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

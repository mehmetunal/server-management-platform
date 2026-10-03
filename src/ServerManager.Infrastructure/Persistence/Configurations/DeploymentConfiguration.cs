using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("Deployments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.ProjectName).HasMaxLength(128).IsRequired();
        builder.Property(d => d.ServerName).HasMaxLength(256).IsRequired();
        builder.Property(d => d.BuildType).HasConversion<int>();
        builder.Property(d => d.Branch).HasMaxLength(200).IsRequired();
        builder.Property(d => d.RequestedCommit).HasMaxLength(64);
        builder.Property(d => d.CommitSha).HasMaxLength(64);
        builder.Property(d => d.CommitMessage).HasMaxLength(500);
        builder.Property(d => d.CommitAuthor).HasMaxLength(256);
        builder.Property(d => d.Status).HasConversion<int>();
        builder.Property(d => d.FailureReason).HasMaxLength(1000);
        builder.Property(d => d.Log).IsRequired();
        builder.Property(d => d.UserId).HasMaxLength(64);
        builder.Property(d => d.UserName).HasMaxLength(256);
        builder.Property(d => d.IpAddress).HasMaxLength(45);
        builder.Property(d => d.CancelledBy).HasMaxLength(256);
    }
}

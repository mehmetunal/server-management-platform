using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Plugin.Git.GitHub.Domain;

namespace ServerManager.Plugin.Git.GitHub.Data;

public class GitHubAppConfiguration : IEntityTypeConfiguration<GitHubApp>
{
    public void Configure(EntityTypeBuilder<GitHubApp> builder)
    {
        builder.ToTable("GitHubApps");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.Property(a => a.Name).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Slug).HasMaxLength(256).IsRequired();
        builder.Property(a => a.OwnerLogin).HasMaxLength(256);
        builder.Property(a => a.HtmlUrl).HasMaxLength(500);
        builder.Property(a => a.ClientId).HasMaxLength(100);
        builder.Property(a => a.EncryptedPrivateKey).IsRequired();
        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);
        builder.Property(a => a.DeletedBy).HasMaxLength(256);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.Name).HasMaxLength(100).IsRequired();
        builder.Property(k => k.Prefix).HasMaxLength(32).IsRequired();
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.Scopes).HasMaxLength(4000).IsRequired();
        builder.Property(k => k.AllowedIps).HasMaxLength(1000);
        builder.Property(k => k.LastUsedIp).HasMaxLength(64);
        builder.Property(k => k.RevokedBy).HasMaxLength(256);
        builder.Property(k => k.CreatedBy).HasMaxLength(256);
        builder.Property(k => k.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(k => k.Prefix).IsUnique();
        builder.HasIndex(k => k.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

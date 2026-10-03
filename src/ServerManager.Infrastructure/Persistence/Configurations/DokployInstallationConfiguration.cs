using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class DokployInstallationConfiguration : IEntityTypeConfiguration<DokployInstallation>
{
    public void Configure(EntityTypeBuilder<DokployInstallation> builder)
    {
        builder.ToTable("DokployInstallations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.ServerName).HasMaxLength(256).IsRequired();
        builder.Property(i => i.UserId).HasMaxLength(64);
        builder.Property(i => i.UserName).HasMaxLength(256);
        builder.Property(i => i.IpAddress).HasMaxLength(45);
        builder.Property(i => i.RequestedVersion).HasMaxLength(64);
        builder.Property(i => i.ScriptUrl).HasMaxLength(500).IsRequired();
        builder.Property(i => i.ScriptSha256).HasMaxLength(64);
        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.FailureReason).HasMaxLength(1000);
        builder.Property(i => i.Output).IsRequired();
    }
}

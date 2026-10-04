using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerConfiguration : IEntityTypeConfiguration<Server>
{
    public void Configure(EntityTypeBuilder<Server> builder)
    {
        builder.ToTable("Servers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Hostname).HasMaxLength(255).IsRequired();
        builder.Property(s => s.IpAddress).HasMaxLength(45).IsRequired();
        builder.Property(s => s.Username).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.Location).HasMaxLength(128);
        builder.Property(s => s.Provider).HasMaxLength(128);
        builder.Property(s => s.OperatingSystem).HasMaxLength(128);
        builder.Property(s => s.HostKeyFingerprint).HasMaxLength(128);
        builder.Property(s => s.LastConnectionMessage).HasMaxLength(500);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);
        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.MonthlyCost).HasPrecision(12, 2);
        builder.Property(s => s.CostCurrency).HasMaxLength(3);

        builder.Property(s => s.AuthenticationType).HasConversion<int>();
        builder.Property(s => s.Environment).HasConversion<int>();
        builder.Property(s => s.Status).HasConversion<int>();

        builder.HasOne(s => s.Credential)
            .WithOne(c => c.Server)
            .HasForeignKey<ServerCredential>(c => c.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Tags)
            .WithOne(t => t.Server)
            .HasForeignKey(t => t.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

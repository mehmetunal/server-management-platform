using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerHealthCheckConfiguration : IEntityTypeConfiguration<ServerHealthCheck>
{
    public void Configure(EntityTypeBuilder<ServerHealthCheck> builder)
    {
        builder.ToTable("ServerHealthChecks");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Property(h => h.Message).HasMaxLength(500);
        builder.HasIndex(h => new { h.ServerId, h.CheckedAt });
    }
}

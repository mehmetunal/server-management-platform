using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerMetricSnapshotConfiguration : IEntityTypeConfiguration<ServerMetricSnapshot>
{
    public void Configure(EntityTypeBuilder<ServerMetricSnapshot> builder)
    {
        builder.ToTable("ServerMetricSnapshots");
        builder.HasKey(s => s.ServerId);
        builder.Property(s => s.ServerId).ValueGeneratedNever();
        builder.Property(s => s.SnapshotJson).IsRequired();
    }
}

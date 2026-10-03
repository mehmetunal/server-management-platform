using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerMetricConfiguration : IEntityTypeConfiguration<ServerMetric>
{
    public void Configure(EntityTypeBuilder<ServerMetric> builder)
    {
        builder.ToTable("ServerMetrics");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.HasIndex(m => new { m.ServerId, m.CollectedAt });
    }
}

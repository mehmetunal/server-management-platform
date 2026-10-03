using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerMetricHourlyConfiguration : IEntityTypeConfiguration<ServerMetricHourly>
{
    public void Configure(EntityTypeBuilder<ServerMetricHourly> builder)
    {
        builder.ToTable("ServerMetricsHourly");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.HasIndex(m => new { m.ServerId, m.HourStart }).IsUnique();
    }
}

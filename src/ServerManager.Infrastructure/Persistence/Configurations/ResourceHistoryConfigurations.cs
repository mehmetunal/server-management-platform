using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ContainerMetricSampleConfiguration : IEntityTypeConfiguration<ContainerMetricSample>
{
    public void Configure(EntityTypeBuilder<ContainerMetricSample> builder)
    {
        builder.ToTable("ContainerMetricSamples");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.ContainerName).HasMaxLength(256).IsRequired();
        builder.Property(m => m.State).HasMaxLength(32).IsRequired();
        builder.Property(m => m.Health).HasMaxLength(32);
        builder.HasIndex(m => new { m.ServerId, m.CollectedAt });
    }
}

public class ContainerMetricHourlyConfiguration : IEntityTypeConfiguration<ContainerMetricHourly>
{
    public void Configure(EntityTypeBuilder<ContainerMetricHourly> builder)
    {
        builder.ToTable("ContainerMetricsHourly");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.ContainerName).HasMaxLength(256).IsRequired();
        builder.HasIndex(m => new { m.ServerId, m.HourStart, m.ContainerName }).IsUnique();
    }
}

public class ProcessSnapshotConfiguration : IEntityTypeConfiguration<ProcessSnapshot>
{
    public void Configure(EntityTypeBuilder<ProcessSnapshot> builder)
    {
        builder.ToTable("ProcessSnapshots");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.ProcessesJson).IsRequired();
        builder.HasIndex(m => new { m.ServerId, m.CollectedAt });
    }
}

public class ServerReclaimableSpaceConfiguration : IEntityTypeConfiguration<ServerReclaimableSpace>
{
    public void Configure(EntityTypeBuilder<ServerReclaimableSpace> builder)
    {
        builder.ToTable("ServerReclaimableSpace");
        builder.HasKey(m => m.ServerId);
        builder.Property(m => m.ServerId).ValueGeneratedNever();
    }
}

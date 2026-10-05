using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ManagedServiceConfiguration : IEntityTypeConfiguration<ManagedService>
{
    public void Configure(EntityTypeBuilder<ManagedService> builder)
    {
        builder.ToTable("ManagedServices");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Slug).HasMaxLength(48).IsRequired();
        builder.Property(s => s.TemplateKey).HasMaxLength(32).IsRequired();
        builder.Property(s => s.ImageTag).HasMaxLength(128).IsRequired();
        builder.Property(s => s.ContainerName).HasMaxLength(64).IsRequired();
        builder.Property(s => s.EncryptedCredentials).IsRequired();
        builder.Property(s => s.PortBindings).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.AllowedSourceIps).HasMaxLength(4000);
        builder.Property(s => s.VolumeMode).HasConversion<int>();
        builder.Property(s => s.HostDataPath).HasMaxLength(500);
        builder.Property(s => s.CpuLimit).HasPrecision(6, 2);
        builder.Property(s => s.Networks).HasMaxLength(500);
        builder.Property(s => s.Status).HasConversion<int>();
        builder.Property(s => s.LastError).HasMaxLength(1000);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);
        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        builder.HasOne(s => s.Server)
            .WithMany()
            .HasForeignKey(s => s.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class ManagedServiceOperationConfiguration : IEntityTypeConfiguration<ManagedServiceOperation>
{
    public void Configure(EntityTypeBuilder<ManagedServiceOperation> builder)
    {
        builder.ToTable("ManagedServiceOperations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.ServiceName).HasMaxLength(64).IsRequired();
        builder.Property(o => o.Kind).HasConversion<int>();
        builder.Property(o => o.Status).HasConversion<int>();
        builder.Property(o => o.Stage).HasMaxLength(32);
        builder.Property(o => o.FromTag).HasMaxLength(128);
        builder.Property(o => o.ToTag).HasMaxLength(128);
        builder.Property(o => o.FailureReason).HasMaxLength(1000);
        builder.Property(o => o.Log).IsRequired();
        builder.Property(o => o.UserId).HasMaxLength(64);
        builder.Property(o => o.UserName).HasMaxLength(256);
        builder.Property(o => o.IpAddress).HasMaxLength(45);
    }
}

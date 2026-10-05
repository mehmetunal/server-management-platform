using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class DeploymentDomainConfiguration : IEntityTypeConfiguration<DeploymentDomain>
{
    public void Configure(EntityTypeBuilder<DeploymentDomain> builder)
    {
        builder.ToTable("DeploymentDomains");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Host).HasMaxLength(253).IsRequired();
        builder.Property(d => d.Path).HasMaxLength(200).IsRequired();
        builder.Property(d => d.ServiceName).HasMaxLength(63);
        builder.Property(d => d.TlsMode).HasConversion<int>();
        builder.Property(d => d.DeletedBy).HasMaxLength(256);
        builder.Property(d => d.CreatedBy).HasMaxLength(256);
        builder.Property(d => d.UpdatedBy).HasMaxLength(256);

        // M019'daki filtreli benzersiz indeksin karşılığı: bir sunucuda aynı host ve yol yalnızca bir domainde olabilir.
        builder.HasIndex(d => new { d.ServerId, d.Host, d.Path })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_DeploymentDomains_Server_Host_Path");

        builder.HasOne(d => d.Project)
            .WithMany()
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class ServerCredentialConfiguration : IEntityTypeConfiguration<ServerCredential>
{
    public void Configure(EntityTypeBuilder<ServerCredential> builder)
    {
        builder.ToTable("ServerCredentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.ServerId).IsUnique();

        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasQueryFilter(c => !c.Server!.IsDeleted);
    }
}

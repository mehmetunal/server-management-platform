using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class CloudAccountConfiguration : IEntityTypeConfiguration<CloudAccount>
{
    public void Configure(EntityTypeBuilder<CloudAccount> builder)
    {
        builder.ToTable("CloudAccounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Provider).HasMaxLength(64).IsRequired();
        builder.Property(a => a.EncryptedToken).IsRequired();
        builder.Property(a => a.AccountLabel).HasMaxLength(200);
        builder.Property(a => a.LastSyncError).HasMaxLength(1000);
        builder.Property(a => a.DeletedBy).HasMaxLength(256);
        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        builder.HasMany(a => a.Servers)
            .WithOne(s => s.CloudAccount)
            .HasForeignKey(s => s.CloudAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}

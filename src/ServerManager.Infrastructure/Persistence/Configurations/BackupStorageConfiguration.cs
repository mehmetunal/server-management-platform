using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class BackupStorageConfiguration : IEntityTypeConfiguration<BackupStorage>
{
    public void Configure(EntityTypeBuilder<BackupStorage> builder)
    {
        builder.ToTable("BackupStorages");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        builder.Property(s => s.ProviderSystemName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.EncryptedSettings).IsRequired();
        builder.Property(s => s.LastError).HasMaxLength(500);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);
        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class NotificationChannelConfiguration : IEntityTypeConfiguration<NotificationChannel>
{
    public void Configure(EntityTypeBuilder<NotificationChannel> builder)
    {
        builder.ToTable("NotificationChannels");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(128).IsRequired();
        builder.Property(c => c.ProviderSystemName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.EncryptedSettings).IsRequired();
        builder.Property(c => c.MinimumSeverity).HasConversion<int>();
        builder.Property(c => c.LastError).HasMaxLength(500);
        builder.Property(c => c.DeletedBy).HasMaxLength(256);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

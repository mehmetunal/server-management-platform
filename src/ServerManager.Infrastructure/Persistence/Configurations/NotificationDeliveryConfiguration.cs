using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.ChannelName).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Kind).HasConversion<int>();
        builder.Property(d => d.Message).HasMaxLength(500);
        builder.HasIndex(d => new { d.ChannelId, d.SentAt });
    }
}

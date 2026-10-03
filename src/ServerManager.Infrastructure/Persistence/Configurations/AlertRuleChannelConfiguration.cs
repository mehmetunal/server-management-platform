using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class AlertRuleChannelConfiguration : IEntityTypeConfiguration<AlertRuleChannel>
{
    public void Configure(EntityTypeBuilder<AlertRuleChannel> builder)
    {
        builder.ToTable("AlertRuleChannels");
        builder.HasKey(c => new { c.RuleId, c.ChannelId });

        builder.HasOne(c => c.Channel)
            .WithMany()
            .HasForeignKey(c => c.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.Channel!.IsDeleted);
    }
}

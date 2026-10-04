using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class CommandRunConfiguration : IEntityTypeConfiguration<CommandRun>
{
    public void Configure(EntityTypeBuilder<CommandRun> builder)
    {
        builder.ToTable("CommandRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Command).HasMaxLength(8000).IsRequired();
        builder.Property(r => r.TemplateName).HasMaxLength(128);
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.UserId).HasMaxLength(450);
        builder.Property(r => r.UserName).HasMaxLength(256);

        builder.HasMany(r => r.Targets)
            .WithOne(t => t.Run)
            .HasForeignKey(t => t.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.StartedAt);
    }
}

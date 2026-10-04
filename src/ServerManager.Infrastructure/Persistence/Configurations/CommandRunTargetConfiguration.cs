using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class CommandRunTargetConfiguration : IEntityTypeConfiguration<CommandRunTarget>
{
    public void Configure(EntityTypeBuilder<CommandRunTarget> builder)
    {
        builder.ToTable("CommandRunTargets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.ServerName).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(t => t.RunId);
    }
}

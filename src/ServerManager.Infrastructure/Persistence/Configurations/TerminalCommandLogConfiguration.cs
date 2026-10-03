using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class TerminalCommandLogConfiguration : IEntityTypeConfiguration<TerminalCommandLog>
{
    public void Configure(EntityTypeBuilder<TerminalCommandLog> builder)
    {
        builder.ToTable("TerminalCommands");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityColumn();

        builder.Property(c => c.CommandText).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.MatchedRule).HasMaxLength(256);

        builder.HasOne<TerminalSessionLog>()
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

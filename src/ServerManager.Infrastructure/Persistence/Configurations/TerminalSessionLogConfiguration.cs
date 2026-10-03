using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Domain.Entities;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class TerminalSessionLogConfiguration : IEntityTypeConfiguration<TerminalSessionLog>
{
    public void Configure(EntityTypeBuilder<TerminalSessionLog> builder)
    {
        builder.ToTable("TerminalSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.ServerName).HasMaxLength(256).IsRequired();
        builder.Property(s => s.UserId).HasMaxLength(64);
        builder.Property(s => s.UserName).HasMaxLength(256);
        builder.Property(s => s.IpAddress).HasMaxLength(45);
        builder.Property(s => s.Kind).HasConversion<int>();
        builder.Property(s => s.Container).HasMaxLength(255);
        builder.Property(s => s.CloseReason).HasMaxLength(256);
    }
}

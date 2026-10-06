using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Infrastructure.Persistence.Configurations;

public class RoleKnownPermissionConfiguration : IEntityTypeConfiguration<RoleKnownPermission>
{
    public void Configure(EntityTypeBuilder<RoleKnownPermission> builder)
    {
        builder.ToTable("RoleKnownPermissions");
        builder.HasKey(p => new { p.RoleId, p.Permission });
        builder.Property(p => p.Permission).HasMaxLength(256);
        builder.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.AspNetCore.Identity;

namespace ServerManager.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}

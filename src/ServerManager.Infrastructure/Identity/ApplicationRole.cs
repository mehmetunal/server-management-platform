using Microsoft.AspNetCore.Identity;

namespace ServerManager.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }

    public string? Description { get; set; }
}

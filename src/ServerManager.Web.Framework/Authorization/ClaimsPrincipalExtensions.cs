using System.Security.Claims;
using ServerManager.Application.Authorization;

namespace ServerManager.Web.Framework.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.Identity?.IsAuthenticated == true
        && (user.IsInRole(Roles.SuperAdmin) || user.HasClaim(Permissions.ClaimType, permission));
}

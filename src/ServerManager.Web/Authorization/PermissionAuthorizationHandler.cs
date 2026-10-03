using Microsoft.AspNetCore.Authorization;
using ServerManager.Application.Authorization;

namespace ServerManager.Web.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (user.IsInRole(Roles.SuperAdmin) || user.HasClaim(Permissions.ClaimType, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

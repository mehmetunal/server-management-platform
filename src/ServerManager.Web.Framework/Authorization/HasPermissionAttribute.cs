using Microsoft.AspNetCore.Authorization;

namespace ServerManager.Web.Framework.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }
}

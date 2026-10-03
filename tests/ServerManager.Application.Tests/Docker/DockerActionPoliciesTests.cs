using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Docker;

namespace ServerManager.Application.Tests.Docker;

public class DockerActionPoliciesTests
{
    [Theory]
    [InlineData(DockerContainerAction.Start, Permissions.DockerStart)]
    [InlineData(DockerContainerAction.Unpause, Permissions.DockerStart)]
    [InlineData(DockerContainerAction.Stop, Permissions.DockerStop)]
    [InlineData(DockerContainerAction.Pause, Permissions.DockerStop)]
    [InlineData(DockerContainerAction.Kill, Permissions.DockerStop)]
    [InlineData(DockerContainerAction.Restart, Permissions.DockerRestart)]
    [InlineData(DockerContainerAction.Remove, Permissions.DockerDelete)]
    public void Maps_actions_to_permissions(DockerContainerAction action, string permission)
    {
        Assert.Equal(permission, DockerActionPolicies.RequiredPermission(action));
    }

    [Fact]
    public void Every_action_has_audit_action_with_display_name()
    {
        foreach (var action in Enum.GetValues<DockerContainerAction>())
        {
            var auditAction = DockerActionPolicies.AuditAction(action);
            Assert.True(AuditActions.DisplayNames.ContainsKey(auditAction), auditAction);
            Assert.NotEqual(action.ToString(), DockerActionPolicies.DisplayName(action));
        }
    }

    [Fact]
    public void Unknown_action_has_no_permission()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DockerActionPolicies.RequiredPermission((DockerContainerAction)99));
    }

    [Fact]
    public void Default_roles_follow_docker_permission_matrix()
    {
        var operatorPermissions = DefaultRolePermissions.Matrix[Roles.Operator];
        Assert.Contains(Permissions.DockerTerminal, operatorPermissions);
        Assert.DoesNotContain(Permissions.DockerDelete, operatorPermissions);
        Assert.DoesNotContain(Permissions.DockerManage, operatorPermissions);

        var developerPermissions = DefaultRolePermissions.Matrix[Roles.Developer];
        Assert.Contains(Permissions.DockerRestart, developerPermissions);
        Assert.DoesNotContain(Permissions.DockerStop, developerPermissions);
        Assert.DoesNotContain(Permissions.DockerTerminal, developerPermissions);

        var viewerPermissions = DefaultRolePermissions.Matrix[Roles.Viewer];
        Assert.Contains(Permissions.DockerView, viewerPermissions);
        Assert.DoesNotContain(Permissions.DockerStart, viewerPermissions);
    }
}

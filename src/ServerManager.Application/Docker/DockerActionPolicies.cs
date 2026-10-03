using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;

namespace ServerManager.Application.Docker;

public static class DockerActionPolicies
{
    public static string RequiredPermission(DockerContainerAction action) => action switch
    {
        DockerContainerAction.Start or DockerContainerAction.Unpause => Permissions.DockerStart,
        DockerContainerAction.Stop or DockerContainerAction.Pause or DockerContainerAction.Kill => Permissions.DockerStop,
        DockerContainerAction.Restart => Permissions.DockerRestart,
        DockerContainerAction.Remove => Permissions.DockerDelete,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static string AuditAction(DockerContainerAction action) => action switch
    {
        DockerContainerAction.Start => AuditActions.DockerContainerStart,
        DockerContainerAction.Stop => AuditActions.DockerContainerStop,
        DockerContainerAction.Restart => AuditActions.DockerContainerRestart,
        DockerContainerAction.Pause => AuditActions.DockerContainerPause,
        DockerContainerAction.Unpause => AuditActions.DockerContainerUnpause,
        DockerContainerAction.Kill => AuditActions.DockerContainerKill,
        DockerContainerAction.Remove => AuditActions.DockerContainerRemove,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static string DisplayName(DockerContainerAction action) => action switch
    {
        DockerContainerAction.Start => "başlatıldı",
        DockerContainerAction.Stop => "durduruldu",
        DockerContainerAction.Restart => "yeniden başlatıldı",
        DockerContainerAction.Pause => "duraklatıldı",
        DockerContainerAction.Unpause => "devam ettirildi",
        DockerContainerAction.Kill => "kill edildi",
        DockerContainerAction.Remove => "silindi",
        _ => action.ToString()
    };
}

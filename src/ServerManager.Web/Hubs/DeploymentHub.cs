using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Authorization;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Deployments;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Hubs;

/// <summary>Deployment çıktısını canlı yayınlar. İstemci yalnızca dinler; iptal HTTP üzerinden yapılır.</summary>
[Authorize]
public sealed class DeploymentHub : Hub
{
    public const string Path = "/hubs/deployments";
    public const string OutputEvent = "deploymentOutput";
    public const string StageEvent = "deploymentStage";
    public const string CompletedEvent = "deploymentCompleted";
    public const string CommitEvent = "deploymentCommit";

    private readonly DeploymentManager _manager;
    private readonly IDeploymentService _deploymentService;

    public DeploymentHub(DeploymentManager manager, IDeploymentService deploymentService)
    {
        _manager = manager;
        _deploymentService = deploymentService;
    }

    public static string GroupName(Guid deploymentId) => $"deployment:{deploymentId:N}";

    [HasPermission(Permissions.DeploymentView)]
    public async Task<DeploymentJoinResponse> JoinDeployment(Guid deploymentId)
    {
        // Gruba önce katılınır; anlık görüntü ile canlı akış arasındaki boşluk sıra numarasıyla kapatılır.
        var group = GroupName(deploymentId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        var live = _manager.Snapshot(deploymentId);
        if (live is not null)
            return live;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        var stored = await _deploymentService.GetAsync(deploymentId, includeLog: true, Context.ConnectionAborted);
        if (!stored.IsSuccess)
            return new DeploymentJoinResponse(false, stored.Message ?? "Deployment kaydı bulunamadı.");

        var deployment = stored.Data!;
        var stage = deployment.Status == DeploymentStatus.Succeeded
            ? DeploymentStage.Completed
            : deployment.DeployStartedAt is not null
                ? DeploymentStage.Deploying
                : deployment.BuildStartedAt is not null
                    ? DeploymentStage.Building
                    : deployment.CommitSha is not null ? DeploymentStage.Source : DeploymentStage.Preparing;

        var message = deployment.IsRunning
            ? "Deployment bu uygulama örneği tarafından izlenmiyor; durum güncellenemiyor."
            : deployment.Status == DeploymentStatus.Succeeded ? "Deployment başarıyla tamamlandı." : deployment.FailureReason;

        return new DeploymentJoinResponse(
            true,
            message,
            deployment.Log,
            long.MaxValue,
            stage.ToString(),
            IsCompleted: true,
            deployment.Status.ToString());
    }

    public Task LeaveDeployment(Guid deploymentId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(deploymentId));
}

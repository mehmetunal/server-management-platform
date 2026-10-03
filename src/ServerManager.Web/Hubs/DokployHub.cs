using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Authorization;
using ServerManager.Application.Dokploy;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.Web.Authorization;
using ServerManager.Web.Dokploy;

namespace ServerManager.Web.Hubs;

/// <summary>Dokploy kurulum çıktısını canlı yayınlar. İstemci yalnızca dinler; kurulumu etkileyemez.</summary>
[Authorize]
public sealed class DokployHub : Hub
{
    public const string Path = "/hubs/dokploy";
    public const string OutputEvent = "installOutput";
    public const string StageEvent = "installStage";
    public const string CompletedEvent = "installCompleted";

    private readonly DokployInstallationManager _manager;
    private readonly IDokployService _dokployService;

    public DokployHub(DokployInstallationManager manager, IDokployService dokployService)
    {
        _manager = manager;
        _dokployService = dokployService;
    }

    public static string GroupName(Guid installationId) => $"dokploy-install:{installationId:N}";

    [HasPermission(Permissions.DokployView)]
    public async Task<DokployJoinResponse> JoinInstallation(Guid serverId, Guid installationId)
    {
        // Gruba önce katılınır; anlık görüntü ile canlı akış arasındaki boşluk sıra numarasıyla kapatılır.
        var group = GroupName(installationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        var live = _manager.Snapshot(serverId, installationId);
        if (live is not null)
            return live;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        var stored = await _dokployService.GetInstallationAsync(serverId, installationId, Context.ConnectionAborted);
        if (!stored.IsSuccess)
            return new DokployJoinResponse(false, stored.Message ?? "Kurulum kaydı bulunamadı.");

        var installation = stored.Data!;
        var succeeded = installation.Status == DokployInstallationStatus.Succeeded;
        var message = installation.Status switch
        {
            DokployInstallationStatus.Succeeded => "Kurulum tamamlandı.",
            DokployInstallationStatus.Running => "Kurulum bu uygulama örneği tarafından izlenmiyor; durum güncellenemiyor.",
            _ => installation.FailureReason ?? "Kurulum başarısız oldu."
        };

        return new DokployJoinResponse(
            true,
            message,
            installation.Output ?? string.Empty,
            long.MaxValue,
            (succeeded ? DokployInstallStage.Completed : DokployInstallStage.Failed).ToString(),
            message,
            IsCompleted: true,
            Succeeded: succeeded);
    }

    public Task LeaveInstallation(Guid installationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(installationId));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Web.Framework.Authorization;
using ServerManager.Web.ManagedServices;

namespace ServerManager.Web.Hubs;

/// <summary>Servis işlemlerinin (kurulum, yükseltme, kaldırma) çıktısını canlı yayınlar. İstemci yalnızca dinler.</summary>
[Authorize]
public sealed class ServiceHub : Hub
{
    public const string Path = "/hubs/services";
    public const string OutputEvent = "serviceOutput";
    public const string StageEvent = "serviceStage";
    public const string CompletedEvent = "serviceCompleted";

    private readonly ManagedServiceManager _manager;
    private readonly IManagedServiceService _services;

    public ServiceHub(ManagedServiceManager manager, IManagedServiceService services)
    {
        _manager = manager;
        _services = services;
    }

    public static string GroupName(Guid operationId) => $"service-operation:{operationId:N}";

    [HasPermission(Permissions.ServicesView)]
    public async Task<ServiceOperationJoinResponse> JoinOperation(Guid operationId)
    {
        // Gruba önce katılınır; anlık görüntü ile canlı akış arasındaki boşluk sıra numarasıyla kapatılır.
        var group = GroupName(operationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        var live = _manager.Snapshot(operationId);
        if (live is not null)
            return live;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        var stored = await _services.GetOperationAsync(operationId, includeLog: true, Context.ConnectionAborted);
        if (!stored.IsSuccess)
            return new ServiceOperationJoinResponse(false, stored.Message ?? "İşlem kaydı bulunamadı.");

        var operation = stored.Data!;
        var stage = operation.Status == ManagedServiceOperationStatus.Succeeded
            ? ServiceOperationStage.Completed.ToString()
            : operation.Stage ?? ServiceOperationStage.Docker.ToString();
        var message = operation.IsRunning
            ? "İşlem bu uygulama örneği tarafından izlenmiyor; durum güncellenemiyor."
            : operation.Status == ManagedServiceOperationStatus.Succeeded ? "İşlem başarıyla tamamlandı." : operation.FailureReason;

        return new ServiceOperationJoinResponse(true, message, operation.Log, long.MaxValue, stage, IsCompleted: true, operation.Status.ToString());
    }

    public Task LeaveOperation(Guid operationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(operationId));
}

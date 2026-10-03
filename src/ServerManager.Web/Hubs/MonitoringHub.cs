using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Authorization;
using ServerManager.Web.Authorization;

namespace ServerManager.Web.Hubs;

[HasPermission(Permissions.ServerView)]
public sealed class MonitoringHub : Hub
{
    public const string Path = "/hubs/monitoring";
    public const string FleetGroup = "fleet";
    public const string ServerUpdatedEvent = "serverUpdated";

    public static string ServerGroup(Guid serverId) => $"server-{serverId:N}";

    public Task JoinServer(Guid serverId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ServerGroup(serverId));

    public Task LeaveServer(Guid serverId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ServerGroup(serverId));

    public Task JoinFleet() =>
        Groups.AddToGroupAsync(Context.ConnectionId, FleetGroup);
}

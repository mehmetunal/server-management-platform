using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Authorization;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Web.Authorization;
using ServerManager.Web.Models;
using ServerManager.Web.Services;

namespace ServerManager.Web.Hubs;

[HasPermission(Permissions.DockerTerminal)]
public sealed class ContainerTerminalHub : Hub
{
    public const string Path = "/hubs/terminal";
    public const string OutputEvent = "output";
    public const string ClosedEvent = "closed";

    // SignalR'ın varsayılan 32 KB mesaj sınırının altında kalır; istemci uzun yapıştırmaları parçalar.
    public const int MaxInputLength = 8192;

    private readonly ContainerTerminalManager _manager;
    private readonly IDockerService _dockerService;

    public ContainerTerminalHub(ContainerTerminalManager manager, IDockerService dockerService)
    {
        _manager = manager;
        _dockerService = dockerService;
    }

    public async Task<TerminalStartResponse> Start(Guid serverId, string container, int columns, int rows)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return new TerminalStartResponse(false, "Oturum bilgisi okunamadı.");

        var result = await _manager.StartAsync(
            Context.ConnectionId,
            userId,
            Context.User?.Identity?.Name,
            _dockerService,
            serverId,
            container ?? string.Empty,
            columns,
            rows,
            Context.ConnectionAborted);

        return new TerminalStartResponse(result.IsSuccess, result.Message);
    }

    public Task Input(string data)
    {
        if (string.IsNullOrEmpty(data) || data.Length > MaxInputLength)
            return Task.CompletedTask;

        return _manager.WriteAsync(Context.ConnectionId, data);
    }

    public void Resize(int columns, int rows) =>
        _manager.Resize(Context.ConnectionId, columns, rows);

    public Task Stop() =>
        _manager.StopAsync(Context.ConnectionId, "Oturum kullanıcı tarafından kapatıldı.");

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _manager.StopAsync(Context.ConnectionId, "Tarayıcı bağlantısı kapandı.");
        await base.OnDisconnectedAsync(exception);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Authorization;
using ServerManager.Web.Terminal;
using ServerManager.Web.Framework.Authorization;

namespace ServerManager.Web.Hubs;

/// <summary>
/// Tarayıcı ile SSH terminal oturumları arasındaki köprü. Tek bağlantı üzerinden birden fazla oturum (sekme) taşınır;
/// tüm olaylar oturum kimliğiyle gönderilir.
/// </summary>
[Authorize]
public sealed class TerminalHub : Hub
{
    public const string Path = "/hubs/terminal";
    public const string OutputEvent = "output";
    public const string ClosedEvent = "closed";
    public const string DetachedEvent = "detached";
    public const string ConfirmEvent = "confirm";
    public const string NoticeEvent = "notice";
    public const string CommandEvent = "command";

    // SignalR'ın varsayılan 32 KB mesaj sınırının altında kalır; istemci uzun yapıştırmaları parçalar.
    public const int MaxInputLength = 8192;

    private readonly TerminalManager _manager;

    public TerminalHub(TerminalManager manager)
    {
        _manager = manager;
    }

    [HasPermission(Permissions.TerminalExecute)]
    public async Task<TerminalStartResponse> StartServer(Guid serverId, int columns, int rows)
    {
        if (CurrentUser() is not { } user)
            return new TerminalStartResponse(false, "Oturum bilgisi okunamadı.");

        var result = await _manager.StartServerAsync(user, serverId, columns, rows, Context.ConnectionAborted);
        return new TerminalStartResponse(result.IsSuccess, result.Message, result.IsSuccess ? result.Data : null);
    }

    [HasPermission(Permissions.DockerTerminal)]
    public async Task<TerminalStartResponse> StartContainer(Guid serverId, string container, int columns, int rows)
    {
        if (CurrentUser() is not { } user)
            return new TerminalStartResponse(false, "Oturum bilgisi okunamadı.");

        var result = await _manager.StartContainerAsync(user, serverId, container ?? string.Empty, columns, rows, Context.ConnectionAborted);
        return new TerminalStartResponse(result.IsSuccess, result.Message, result.IsSuccess ? result.Data : null);
    }

    public Task<TerminalAttachResponse> Attach(Guid sessionId, int columns, int rows) =>
        CurrentUser() is { } user
            ? _manager.AttachAsync(user, sessionId, columns, rows)
            : Task.FromResult(new TerminalAttachResponse(false, "Oturum bilgisi okunamadı."));

    public Task Input(Guid sessionId, string data)
    {
        if (string.IsNullOrEmpty(data) || data.Length > MaxInputLength)
            return Task.CompletedTask;

        return _manager.WriteAsync(Context.ConnectionId, sessionId, data);
    }

    public void Resize(Guid sessionId, int columns, int rows) =>
        _manager.Resize(Context.ConnectionId, sessionId, columns, rows);

    public Task Stop(Guid sessionId) =>
        _manager.StopAsync(Context.ConnectionId, sessionId);

    public Task Confirm(Guid sessionId, string token, bool approve) =>
        _manager.ResolveConfirmationAsync(Context.ConnectionId, sessionId, token ?? string.Empty, approve);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _manager.Detach(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private TerminalUser? CurrentUser()
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            return null;

        return new TerminalUser(
            Context.ConnectionId,
            userId,
            Context.User?.Identity?.Name,
            Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString());
    }
}

using Microsoft.AspNetCore.SignalR;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.Hubs;
using ServerManager.Plugin.DevOps.Dokploy.Services;

namespace ServerManager.Plugin.DevOps.Dokploy.Installation;

/// <summary>
/// Kurulum çıktısını bellekteki kayda yazar ve izleyen tarayıcılara iletir.
/// Gönderim hataları yutulur: tarayıcı bağlantısındaki bir sorun sunucudaki kurulumu yarıda kesmemeli.
/// </summary>
public sealed class DokployInstallationSink : IDokployInstallObserver
{
    private readonly DokployInstallationRun _run;
    private readonly IHubContext<DokployHub> _hubContext;
    private readonly ILogger _logger;

    public DokployInstallationSink(DokployInstallationRun run, IHubContext<DokployHub> hubContext, ILogger logger)
    {
        _run = run;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task OnOutputAsync(string text, CancellationToken cancellationToken)
    {
        var sequence = _run.AppendOutput(text);
        return SendAsync(DokployHub.OutputEvent, _run.Id, sequence, text);
    }

    public Task OnStageAsync(DokployInstallStage stage, string message, CancellationToken cancellationToken)
    {
        _run.SetStage(stage, message);
        return SendAsync(DokployHub.StageEvent, _run.Id, stage.ToString(), message);
    }

    public Task CompletedAsync(bool succeeded, string? message) =>
        SendAsync(DokployHub.CompletedEvent, _run.Id, succeeded, message);

    private async Task SendAsync(string method, params object?[] args)
    {
        try
        {
            await _hubContext.Clients.Group(DokployHub.GroupName(_run.Id)).SendCoreAsync(method, args, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dokploy kurulum olayı gönderilemedi. InstallationId: {InstallationId}", _run.Id);
        }
    }
}

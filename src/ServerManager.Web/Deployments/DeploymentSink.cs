using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Domain.Enums;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.Deployments;

/// <summary>
/// Deployment çıktısını bellekteki kayda yazar ve izleyen tarayıcılara iletir.
/// Gönderim hataları yutulur: tarayıcı bağlantısındaki bir sorun sunucudaki deployment'ı yarıda kesmemeli.
/// </summary>
public sealed class DeploymentSink : IDeploymentObserver
{
    private readonly DeploymentRun _run;
    private readonly IHubContext<DeploymentHub> _hubContext;
    private readonly ILogger _logger;

    public DeploymentSink(DeploymentRun run, IHubContext<DeploymentHub> hubContext, ILogger logger)
    {
        _run = run;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task OnOutputAsync(string text, CancellationToken cancellationToken)
    {
        var sequence = _run.AppendOutput(text);
        return SendAsync(DeploymentHub.OutputEvent, _run.Id, sequence, text);
    }

    public Task OnStageAsync(DeploymentStage stage, string message, CancellationToken cancellationToken)
    {
        _run.SetStage(stage);
        return SendAsync(DeploymentHub.StageEvent, _run.Id, stage.ToString(), message);
    }

    public Task OnCommitAsync(DeploymentCommit commit, CancellationToken cancellationToken) =>
        SendAsync(DeploymentHub.CommitEvent, _run.Id, commit.Sha);

    public Task CompletedAsync(DeploymentStatus status, string? message) =>
        SendAsync(DeploymentHub.CompletedEvent, _run.Id, status.ToString(), message);

    private async Task SendAsync(string method, params object?[] args)
    {
        try
        {
            await _hubContext.Clients.Group(DeploymentHub.GroupName(_run.Id)).SendCoreAsync(method, args, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Deployment olayı gönderilemedi. DeploymentId: {DeploymentId}", _run.Id);
        }
    }
}

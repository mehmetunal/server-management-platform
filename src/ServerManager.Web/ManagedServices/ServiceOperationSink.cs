using Microsoft.AspNetCore.SignalR;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Web.Hubs;

namespace ServerManager.Web.ManagedServices;

/// <summary>
/// İşlem çıktısını bellekteki kayda yazar ve izleyen tarayıcılara iletir. Çıktı buraya gelmeden önce maskelenmiştir.
/// Gönderim hataları yutulur: tarayıcı bağlantısındaki sorun sunucudaki işlemi yarıda kesmemeli.
/// </summary>
public sealed class ServiceOperationSink : IServiceOperationObserver
{
    private readonly ServiceOperationRun _run;
    private readonly IHubContext<ServiceHub> _hubContext;
    private readonly ILogger _logger;

    public ServiceOperationSink(ServiceOperationRun run, IHubContext<ServiceHub> hubContext, ILogger logger)
    {
        _run = run;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task OnOutputAsync(string text, CancellationToken cancellationToken)
    {
        var sequence = _run.AppendOutput(text);
        return SendAsync(ServiceHub.OutputEvent, _run.Id, sequence, text);
    }

    public Task OnStageAsync(ServiceOperationStage stage, string message, CancellationToken cancellationToken)
    {
        _run.SetStage(stage);
        return SendAsync(ServiceHub.StageEvent, _run.Id, stage.ToString(), message);
    }

    public Task CompletedAsync(ManagedServiceOperationStatus status, string? message) =>
        SendAsync(ServiceHub.CompletedEvent, _run.Id, status.ToString(), message);

    private async Task SendAsync(string method, params object?[] args)
    {
        try
        {
            await _hubContext.Clients.Group(ServiceHub.GroupName(_run.Id)).SendCoreAsync(method, args, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Servis işlem olayı gönderilemedi. OperationId: {OperationId}", _run.Id);
        }
    }
}

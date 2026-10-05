using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.Interfaces.ManagedServices;

/// <summary>Servis işleminin çıktısını ve aşamalarını izleyen taraf (canlı log, kayıt).</summary>
public interface IServiceOperationObserver
{
    Task OnOutputAsync(string text, CancellationToken cancellationToken);

    Task OnStageAsync(ServiceOperationStage stage, string message, CancellationToken cancellationToken);
}

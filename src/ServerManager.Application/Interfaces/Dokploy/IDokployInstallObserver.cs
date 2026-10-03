using ServerManager.Application.Dokploy;

namespace ServerManager.Application.Interfaces.Dokploy;

/// <summary>Arka planda süren kurulumun çıktısını ve aşamalarını canlı izleyen taraf (ör. SignalR).</summary>
public interface IDokployInstallObserver
{
    Task OnOutputAsync(string text, CancellationToken cancellationToken);

    Task OnStageAsync(DokployInstallStage stage, string message, CancellationToken cancellationToken);
}

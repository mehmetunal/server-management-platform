using ServerManager.Plugin.DevOps.Dokploy.Core;

namespace ServerManager.Plugin.DevOps.Dokploy.Services;

/// <summary>Arka planda süren kurulumun çıktısını ve aşamalarını canlı izleyen taraf (ör. SignalR).</summary>
public interface IDokployInstallObserver
{
    Task OnOutputAsync(string text, CancellationToken cancellationToken);

    Task OnStageAsync(DokployInstallStage stage, string message, CancellationToken cancellationToken);
}

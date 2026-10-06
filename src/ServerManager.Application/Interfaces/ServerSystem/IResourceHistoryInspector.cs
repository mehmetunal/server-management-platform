using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Application.Interfaces.ServerSystem;

/// <summary>Kaynak geçmişi için tek SSH oturumunda process CPU/RSS örneği ile container durum ve kullanımını okur.</summary>
public interface IResourceHistoryInspector
{
    Task<ServiceResult<ResourceHistoryFacts>> CollectAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);
}

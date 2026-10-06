using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Application.Interfaces.ServerSystem;

/// <summary>Kaynak Kullanımı sayfası: yük, bellek, CPU dağılımı, process'ler, diskler ve container istatistiklerini salt okunur toplar.</summary>
public interface IResourceUsageInspector
{
    Task<ServiceResult<ResourceFacts>> GetFactsAsync(RemoteExecutionContext context, bool includeDocker, CancellationToken cancellationToken = default);

    /// <summary>En büyük klasörler (du) ve dosyalar (find); nice/ionice ve süre sınırıyla çalışır.</summary>
    Task<ServiceResult<DiskUsageReport>> ScanDiskAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default);
}

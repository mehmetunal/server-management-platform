using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Application.Interfaces.Backups;

/// <summary>Sunucuda (agentless, SSH) yedek akışını üretir veya geri yükler. Veri sunucuda gzip ile sıkıştırılır.</summary>
public interface IBackupSourceRunner
{
    /// <summary><paramref name="consume"/> sıkıştırılmış akışı sonuna kadar okumalıdır.</summary>
    Task<ServiceResult> ExportAsync(
        ServerConnection connection,
        BackupSourceSpec source,
        Func<Stream, CancellationToken, Task> consume,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary><paramref name="produce"/> sıkıştırılmış akışı yazar; dönünce girdi kapatılır.</summary>
    Task<ServiceResult> ImportAsync(
        ServerConnection connection,
        BackupSourceSpec target,
        Func<Stream, CancellationToken, Task> produce,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

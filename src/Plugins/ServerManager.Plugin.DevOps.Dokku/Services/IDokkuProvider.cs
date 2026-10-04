using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Plugin.DevOps.Dokku.Integration;

namespace ServerManager.Plugin.DevOps.Dokku.Services;

public interface IDokkuProvider
{
    Task<ServiceResult<DokkuReport>> GetReportAsync(RemoteExecutionContext context, TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<ServiceResult> RestartAsync(RemoteExecutionContext context, string app, TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<ServiceResult> InstallAsync(
        RemoteExecutionContext context,
        string version,
        TimeSpan timeout,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken = default);
}

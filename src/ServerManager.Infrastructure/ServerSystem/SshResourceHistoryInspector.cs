using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ResourceUsage;

namespace ServerManager.Infrastructure.ServerSystem;

public sealed class SshResourceHistoryInspector : IResourceHistoryInspector
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromSeconds(60);

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshResourceHistoryInspector> _logger;

    public SshResourceHistoryInspector(IRemoteCommandRunner runner, ILogger<SshResourceHistoryInspector> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<ResourceHistoryFacts>> CollectAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var processOutput = await executor.ExecuteAsync(new RemoteCommand(ResourceHistoryCommands.Processes, ProcessTimeout), ct);
            var dockerOutput = await executor.ExecuteAsync(new RemoteCommand(ResourceHistoryCommands.Docker, DockerTimeout, Elevate: true), ct);

            var processesComplete = ResourceHistoryParser.IsComplete(processOutput.Stdout);
            var dockerComplete = ResourceHistoryParser.IsComplete(dockerOutput.Stdout);
            if (!processesComplete && !dockerComplete)
                return ServiceResult<ResourceHistoryFacts>.Failure("Kaynak geçmişi okunamadı. Sunucunun Linux ve sh kabuğu olduğundan emin olun.");

            try
            {
                var (processes, busy) = processesComplete ? ResourceHistoryParser.ParseProcesses(processOutput.Stdout) : ([], null);
                var dockerAvailable = dockerComplete && ResourceHistoryParser.HasDocker(dockerOutput.Stdout);
                return ServiceResult<ResourceHistoryFacts>.Success(new ResourceHistoryFacts
                {
                    CpuBusyPercent = busy,
                    Processes = processes,
                    DockerAvailable = dockerAvailable,
                    Containers = dockerAvailable ? ResourceHistoryParser.ParseContainers(dockerOutput.Stdout) : []
                });
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException)
            {
                _logger.LogWarning(ex, "Kaynak geçmişi çıktısı okunamadı. Target: {Target}", context);
                return ServiceResult<ResourceHistoryFacts>.Failure("Kaynak geçmişi çıktısı okunamadı.");
            }
        }, cancellationToken);
}

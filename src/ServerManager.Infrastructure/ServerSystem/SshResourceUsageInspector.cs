using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ResourceUsage;
using ServerManager.Application.ServerSystem;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Infrastructure.ServerSystem;

public sealed class SshResourceUsageInspector : IResourceUsageInspector
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromSeconds(45);

    /// <summary>du ve find'ın her biri ScanTimeoutSeconds ile sınırlı; SSH zaman aşımı ikisinin toplamından biraz fazla.</summary>
    private static readonly TimeSpan DiskScanTimeout = TimeSpan.FromSeconds(ResourceRules.ScanTimeoutSeconds * 2 + 30);

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshResourceUsageInspector> _logger;

    public SshResourceUsageInspector(IRemoteCommandRunner runner, ILogger<SshResourceUsageInspector> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<ResourceFacts>> GetFactsAsync(RemoteExecutionContext context, bool includeDocker, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var snapshotOutput = await RunElevatedWithFallbackAsync(executor, context, ResourceCommands.Snapshot, ReadTimeout, ct);
            if (snapshotOutput.TimedOut)
                return ServiceResult<ResourceFacts>.Failure("Kaynak bilgisi okunurken zaman aşımı oldu.");
            if (!ServerSystemParser.IsComplete(snapshotOutput.Stdout))
                return ServiceResult<ResourceFacts>.Failure("Kaynak bilgisi okunamadı. Sunucunun Linux ve sh kabuğu olduğundan emin olun.");

            var processes = await executor.ExecuteAsync(new RemoteCommand(ServerSystemCommands.Processes, ReadTimeout), ct);
            var storage = await executor.ExecuteAsync(new RemoteCommand(ServerSystemCommands.Storage, ReadTimeout), ct);

            IReadOnlyList<ContainerUsageFact>? containers = null;
            string? dockerMessage = null;
            if (includeDocker)
            {
                var stats = await executor.ExecuteAsync(new RemoteCommand(DockerCommands.Stats(null), DockerTimeout, Elevate: true), ct);
                if (stats.IsSuccess)
                {
                    var ps = await executor.ExecuteAsync(new RemoteCommand(DockerCommands.ListContainers, DockerTimeout, Elevate: true), ct);
                    var labels = ps.IsSuccess
                        ? DockerHousekeepingParser.ParseContainerLabels(ps.Stdout)
                        : new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                    containers = DockerHousekeepingParser.JoinStats(DockerOutputParser.ParseStats(stats.Stdout), labels);
                }
                else
                {
                    dockerMessage = stats.ExitCode == 127 || (stats.Stderr + stats.Stdout).Contains("not found", StringComparison.OrdinalIgnoreCase)
                        ? "Bu sunucuda Docker kurulu değil."
                        : DockerErrorTranslator.Translate(stats);
                }
            }

            try
            {
                return ServiceResult<ResourceFacts>.Success(new ResourceFacts
                {
                    Snapshot = ResourceParser.ParseSnapshot(snapshotOutput.Stdout),
                    Processes = ServerSystemParser.IsComplete(processes.Stdout) ? ServerSystemParser.ParseProcesses(processes.Stdout) : null,
                    Storage = ServerSystemParser.IsComplete(storage.Stdout) ? ServerSystemParser.ParseStorage(storage.Stdout) : null,
                    Containers = containers,
                    DockerMessage = dockerMessage
                });
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException)
            {
                _logger.LogWarning(ex, "Kaynak kullanımı çıktısı okunamadı. Target: {Target}", context);
                return ServiceResult<ResourceFacts>.Failure("Kaynak bilgisi çıktısı okunamadı.");
            }
        }, cancellationToken);

    public Task<ServiceResult<DiskUsageReport>> ScanDiskAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default)
    {
        if (!ResourceRules.IsValidScanPath(path))
            return Task.FromResult(ServiceResult<DiskUsageReport>.Failure("Geçersiz yol."));

        return _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await RunElevatedWithFallbackAsync(executor, context, ResourceCommands.DiskScan(path), DiskScanTimeout, ct);
            if (output.TimedOut)
                return ServiceResult<DiskUsageReport>.Failure("Disk taraması zaman aşımına uğradı. Daha dar bir klasör seçin.");
            if (!ServerSystemParser.IsComplete(output.Stdout))
                return ServiceResult<DiskUsageReport>.Failure("Disk taraması yapılamadı.");

            return ServiceResult<DiskUsageReport>.Success(ResourceParser.ParseDiskScan(path, output.Stdout));
        }, cancellationToken);
    }

    private static async Task<RemoteCommandOutput> RunElevatedWithFallbackAsync(
        IRemoteCommandExecutor executor, RemoteExecutionContext context, string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (context.UseSudo)
        {
            var elevated = await executor.ExecuteAsync(new RemoteCommand(command, timeout, Elevate: true), cancellationToken);
            if (elevated.TimedOut || ServerSystemParser.IsComplete(elevated.Stdout))
                return elevated;
        }

        return await executor.ExecuteAsync(new RemoteCommand(command, timeout), cancellationToken);
    }
}

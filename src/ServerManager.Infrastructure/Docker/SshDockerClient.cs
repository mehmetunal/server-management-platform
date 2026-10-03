using Microsoft.Extensions.Options;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Docker;

public sealed class SshDockerClient : IDockerClient
{
    private readonly IRemoteCommandRunner _runner;
    private readonly ITerminalSessionFactory _terminalFactory;
    private readonly DockerOptions _options;

    public SshDockerClient(IRemoteCommandRunner runner, ITerminalSessionFactory terminalFactory, IOptions<DockerOptions> options)
    {
        _runner = runner;
        _terminalFactory = terminalFactory;
        _options = options.Value;
    }

    private TimeSpan CommandTimeout => TimeSpan.FromSeconds(Math.Max(5, _options.CommandTimeoutSeconds));

    public Task<ServiceResult<DockerOverviewDto>> GetOverviewAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var info = await RunAsync(executor, DockerCommands.Info, ct);
            if (!info.IsSuccess)
                return Fail<DockerOverviewDto>(info);

            var containers = await RunAsync(executor, DockerCommands.ListContainers, ct);
            if (!containers.IsSuccess)
                return Fail<DockerOverviewDto>(containers);

            var volumes = await RunAsync(executor, DockerCommands.ListVolumeNames, ct);
            var networks = await RunAsync(executor, DockerCommands.ListNetworkIds, ct);
            var diskUsage = await RunAsync(executor, DockerCommands.DiskUsage, ct);

            return ServiceResult<DockerOverviewDto>.Success(DockerOutputParser.ParseOverview(
                info.Stdout,
                containers.Stdout,
                volumes.IsSuccess ? volumes.Stdout : null,
                networks.IsSuccess ? networks.Stdout : null,
                diskUsage.IsSuccess ? diskUsage.Stdout : null));
        }, cancellationToken);

    public Task<ServiceResult<IReadOnlyList<DockerContainerDto>>> GetContainersAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var list = await RunAsync(executor, DockerCommands.ListContainers, ct);
            if (!list.IsSuccess)
                return Fail<IReadOnlyList<DockerContainerDto>>(list);

            var ids = DockerOutputParser.ParseContainerIds(list.Stdout);
            string? inspectOutput = null;
            if (ids.Count > 0)
            {
                var inspect = await RunAsync(executor, DockerCommands.InspectContainers(ids), ct);
                inspectOutput = inspect.IsSuccess ? inspect.Stdout : null;
            }

            return ServiceResult<IReadOnlyList<DockerContainerDto>>.Success(DockerOutputParser.ParseContainers(list.Stdout, inspectOutput));
        }, cancellationToken);

    public Task<ServiceResult<IReadOnlyList<DockerContainerStatsDto>>> GetContainerStatsAsync(RemoteExecutionContext context, string? container, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var stats = await RunAsync(executor, DockerCommands.Stats(container), ct, CommandTimeout + TimeSpan.FromSeconds(10));
            return stats.IsSuccess
                ? ServiceResult<IReadOnlyList<DockerContainerStatsDto>>.Success(DockerOutputParser.ParseStats(stats.Stdout))
                : Fail<IReadOnlyList<DockerContainerStatsDto>>(stats);
        }, cancellationToken);

    public Task<ServiceResult<DockerContainerDetailsDto>> GetContainerAsync(RemoteExecutionContext context, string container, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var inspect = await RunAsync(executor, DockerCommands.InspectContainers([container]), ct);
            if (!inspect.IsSuccess)
            {
                var message = DockerErrorTranslator.Translate(inspect);
                return message == "Container bulunamadı."
                    ? ServiceResult<DockerContainerDetailsDto>.NotFound(message)
                    : ServiceResult<DockerContainerDetailsDto>.Failure(message);
            }

            var details = DockerOutputParser.ParseContainerDetails(inspect.Stdout);
            return details is null
                ? ServiceResult<DockerContainerDetailsDto>.NotFound("Container bulunamadı.")
                : ServiceResult<DockerContainerDetailsDto>.Success(details);
        }, cancellationToken);

    public Task<ServiceResult<DockerLogsDto>> GetContainerLogsAsync(RemoteExecutionContext context, string container, int tail, string? since, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var logs = await RunAsync(executor, DockerCommands.Logs(container, tail, since), ct);
            return logs.IsSuccess
                ? ServiceResult<DockerLogsDto>.Success(DockerOutputParser.ParseLogs(container, logs.Stdout, logs.Stderr, tail))
                : Fail<DockerLogsDto>(logs);
        }, cancellationToken);

    public Task<ServiceResult> ExecuteContainerActionAsync(RemoteExecutionContext context, string container, DockerContainerAction action, bool force, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.ContainerAction(container, action, force), CommandTimeout, cancellationToken);

    public Task<ServiceResult> RenameContainerAsync(RemoteExecutionContext context, string container, string newName, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.Rename(container, newName), CommandTimeout, cancellationToken);

    public Task<ServiceResult<IReadOnlyList<DockerImageDto>>> GetImagesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var images = await RunAsync(executor, DockerCommands.ListImages, ct);
            return images.IsSuccess
                ? ServiceResult<IReadOnlyList<DockerImageDto>>.Success(DockerOutputParser.ParseImages(images.Stdout))
                : Fail<IReadOnlyList<DockerImageDto>>(images);
        }, cancellationToken);

    public Task<ServiceResult> PullImageAsync(RemoteExecutionContext context, string reference, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.Pull(reference), TimeSpan.FromSeconds(Math.Max(30, _options.PullTimeoutSeconds)), cancellationToken);

    public Task<ServiceResult> RemoveImageAsync(RemoteExecutionContext context, string reference, bool force, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.RemoveImage(reference, force), CommandTimeout, cancellationToken);

    public async Task<ServiceResult> PruneImagesAsync(RemoteExecutionContext context, bool all, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            var prune = await RunAsync(executor, DockerCommands.PruneImages(all), ct, CommandTimeout * 3);
            if (!prune.IsSuccess)
                return Fail<string>(prune);

            var reclaimed = prune.Stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => line.StartsWith("Total reclaimed space:", StringComparison.OrdinalIgnoreCase));
            var size = reclaimed?["Total reclaimed space:".Length..].Trim();
            return ServiceResult<string>.Success(size ?? "0B");
        }, cancellationToken);

        return result.IsSuccess
            ? ServiceResult.Success($"Kullanılmayan image'lar temizlendi. Kazanılan alan: {result.Data}.")
            : ServiceResult.Failure(result.Message ?? "Image temizliği başarısız.");
    }

    public Task<ServiceResult<IReadOnlyList<DockerVolumeDto>>> GetVolumesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var volumes = await RunAsync(executor, DockerCommands.ListVolumes, ct);
            if (!volumes.IsSuccess)
                return Fail<IReadOnlyList<DockerVolumeDto>>(volumes);

            var containers = await RunAsync(executor, DockerCommands.ListContainers, ct);
            var usage = await RunAsync(executor, DockerCommands.DiskUsageVerbose, ct);

            return ServiceResult<IReadOnlyList<DockerVolumeDto>>.Success(DockerOutputParser.ParseVolumes(
                volumes.Stdout,
                containers.IsSuccess ? containers.Stdout : null,
                usage.IsSuccess ? usage.Stdout : null));
        }, cancellationToken);

    public Task<ServiceResult> CreateVolumeAsync(RemoteExecutionContext context, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.CreateVolume(name), CommandTimeout, cancellationToken);

    public Task<ServiceResult> RemoveVolumeAsync(RemoteExecutionContext context, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.RemoveVolume(name), CommandTimeout, cancellationToken);

    public Task<ServiceResult<IReadOnlyList<DockerNetworkDto>>> GetNetworksAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var networks = await RunAsync(executor, DockerCommands.ListNetworks, ct);
            if (!networks.IsSuccess)
                return Fail<IReadOnlyList<DockerNetworkDto>>(networks);

            var ids = DockerOutputParser.ParseNetworkIds(networks.Stdout);
            string? inspectOutput = null;
            if (ids.Count > 0)
            {
                var inspect = await RunAsync(executor, DockerCommands.InspectNetworks(ids), ct);
                inspectOutput = inspect.IsSuccess ? inspect.Stdout : null;
            }

            return ServiceResult<IReadOnlyList<DockerNetworkDto>>.Success(DockerOutputParser.ParseNetworks(networks.Stdout, inspectOutput));
        }, cancellationToken);

    public async Task<ServiceResult> CreateNetworkAsync(RemoteExecutionContext context, CreateNetworkDto dto, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            if (!string.IsNullOrWhiteSpace(dto.Subnet))
            {
                var probe = await executor.ExecuteAsync(new RemoteCommand(HostNetworkGuard.ProbeCommand, CommandTimeout, Elevate: false), ct);
                if (probe.IsSuccess && HostNetworkGuard.FindConflict(dto.Subnet, probe.Stdout) is { } conflict)
                    return ServiceResult<bool>.Failure($"{conflict} Bu network sunucuya erişimi keseceği için oluşturulmadı; farklı bir subnet seçin.");
            }

            var output = await RunAsync(executor, DockerCommands.CreateNetwork(dto), ct);
            return output.IsSuccess ? ServiceResult<bool>.Success(true) : Fail<bool>(output);
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.Message ?? "Docker işlemi başarısız.");
    }

    public Task<ServiceResult> RemoveNetworkAsync(RemoteExecutionContext context, string network, CancellationToken cancellationToken = default) =>
        MutateAsync(context, DockerCommands.RemoveNetwork(network), CommandTimeout, cancellationToken);

    public Task<ServiceResult<ITerminalSession>> OpenContainerTerminalAsync(
        RemoteExecutionContext context,
        string container,
        int columns,
        int rows,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default) =>
        _terminalFactory.OpenAsync(new TerminalOpenRequest
        {
            Context = context,
            Command = DockerCommands.ExecShell(container),
            Elevate = true,
            Columns = columns,
            Rows = rows
        }, sink, cancellationToken);

    private async Task<ServiceResult> MutateAsync(RemoteExecutionContext context, string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await RunAsync(executor, command, ct, timeout);
            return output.IsSuccess ? ServiceResult<bool>.Success(true) : Fail<bool>(output);
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.Message ?? "Docker işlemi başarısız.");
    }

    private Task<RemoteCommandOutput> RunAsync(IRemoteCommandExecutor executor, string command, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        executor.ExecuteAsync(new RemoteCommand(command, timeout ?? CommandTimeout, Elevate: true), cancellationToken);

    private static ServiceResult<T> Fail<T>(RemoteCommandOutput output) =>
        ServiceResult<T>.Failure(DockerErrorTranslator.Translate(output));
}

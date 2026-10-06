using System.Globalization;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Infrastructure.ServerSystem;

public sealed class SshServerCleanupInspector : IServerCleanupInspector
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan DockerScanTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ShortStepTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LongStepTimeout = TimeSpan.FromMinutes(10);
    private const int MaxStepOutput = 2000;

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshServerCleanupInspector> _logger;

    public SshServerCleanupInspector(IRemoteCommandRunner runner, ILogger<SshServerCleanupInspector> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<CleanupFacts>> ScanAsync(RemoteExecutionContext context, CleanupOptions options, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var system = await RunScriptAsync(executor, context, CleanupCommands.Scan(options), ct);
            if (system.TimedOut)
                return ServiceResult<CleanupFacts>.Failure("Sunucu taranırken zaman aşımı oldu.");
            if (!ServerSystemParser.IsComplete(system.Stdout))
                return ServiceResult<CleanupFacts>.Failure("Sunucu taranamadı. Sunucunun Linux ve sh kabuğu olduğundan emin olun.");

            var df = await executor.ExecuteAsync(new RemoteCommand(DockerCommands.DiskUsageVerbose, DockerScanTimeout, Elevate: true), ct);
            DockerHousekeepingParser.DiskUsage? usage = null;
            string? dockerMessage = null;
            IReadOnlyList<DockerNetworkFact> networks = [];
            if (df.IsSuccess)
            {
                usage = DockerHousekeepingParser.ParseDiskUsage(df.Stdout);
                var networkOutput = await executor.ExecuteAsync(new RemoteCommand(DockerCommands.ListUnusedNetworks, ShortStepTimeout, Elevate: true), ct);
                networks = networkOutput.IsSuccess ? DockerHousekeepingParser.ParseNetworks(networkOutput.Stdout) : [];
            }
            else
            {
                dockerMessage = IsDockerMissing(df)
                    ? "Bu sunucuda Docker kurulu değil."
                    : DockerErrorTranslator.Translate(df);
            }

            try
            {
                var facts = CleanupParser.ParseSystem(system.Stdout);
                return ServiceResult<CleanupFacts>.Success(new CleanupFacts
                {
                    DockerAvailable = usage is not null,
                    DockerMessage = dockerMessage,
                    Containers = usage?.Containers ?? [],
                    Images = usage?.Images ?? [],
                    Volumes = usage?.Volumes ?? [],
                    UnusedNetworks = networks,
                    BuildCacheReclaimableBytes = usage?.BuildCacheReclaimableBytes ?? 0,
                    BuildCacheEntries = usage?.BuildCacheEntries ?? 0,
                    PackageManager = facts.PackageManager,
                    PackageCacheBytes = facts.PackageCacheBytes,
                    JournalAvailable = facts.JournalAvailable,
                    JournalBytes = facts.JournalBytes,
                    RotatedLogs = facts.RotatedLogs,
                    TempFiles = facts.TempFiles,
                    SnapAvailable = facts.SnapAvailable,
                    DisabledSnaps = facts.DisabledSnaps,
                    RunningKernel = facts.RunningKernel,
                    Kernels = facts.Kernels
                });
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException)
            {
                _logger.LogWarning(ex, "Temizlik tarama çıktısı okunamadı. Target: {Target}", context);
                return ServiceResult<CleanupFacts>.Failure("Tarama çıktısı okunamadı.");
            }
        }, cancellationToken);

    public Task<ServiceResult<CleanupRunResult>> ExecuteAsync(
        RemoteExecutionContext context,
        IReadOnlyList<CleanupItem> items,
        CleanupOptions options,
        bool dryRun,
        Func<CleanupLogEntry, CancellationToken, Task> onLog,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            long? before = dryRun ? null : await AvailableBytesAsync(executor, ct);
            var succeeded = 0;
            var failed = 0;
            var processed = new List<string>();

            if (dryRun)
                await onLog(new CleanupLogEntry(CleanupLogLevel.Info, "Önizleme: hiçbir şey silinmeyecek."), ct);
            else if (context.UseSudo)
                await onLog(new CleanupLogEntry(CleanupLogLevel.Info, "Komutlar sudo ile çalıştırılıyor."), ct);

            foreach (var item in items.OrderBy(i => i.Category))
            {
                var step = BuildStep(item, options, dryRun);
                if (step is null)
                {
                    await onLog(new CleanupLogEntry(CleanupLogLevel.Warning, $"{item.Name}: bu öğe panelden silinemez; atlandı.", Key: item.Key), ct);
                    continue;
                }

                if (step.Command is null)
                {
                    succeeded++;
                    await onLog(new CleanupLogEntry(CleanupLogLevel.Info, $"{Label(item)} — çalıştırılacak komut: {step.Description}", Key: item.Key), ct);
                    continue;
                }

                await onLog(new CleanupLogEntry(CleanupLogLevel.Info, $"{Label(item)} — {step.Description}", Key: item.Key), ct);
                var output = await executor.ExecuteAsync(new RemoteCommand(step.Command, step.Timeout, Elevate: true), ct);
                var text = TextHelper.Truncate((output.Stdout + output.Stderr).Trim(), MaxStepOutput);
                if (output.IsSuccess)
                {
                    succeeded++;
                    processed.Add(item.Name);
                    await onLog(new CleanupLogEntry(CleanupLogLevel.Success, dryRun ? $"{item.Name}: önizleme hazır." : $"{item.Name}: tamamlandı.",
                        string.IsNullOrEmpty(text) ? null : text, item.Key), ct);
                }
                else
                {
                    failed++;
                    var message = output.TimedOut
                        ? "zaman aşımı"
                        : step.IsDocker ? DockerErrorTranslator.Translate(output) : (string.IsNullOrEmpty(text) ? $"komut {output.ExitCode?.ToString(CultureInfo.InvariantCulture)} koduyla bitti" : text);
                    await onLog(new CleanupLogEntry(CleanupLogLevel.Error, $"{item.Name}: başarısız — {message}", Key: item.Key), ct);
                }
            }

            long? freed = null;
            if (!dryRun && before is not null && await AvailableBytesAsync(executor, ct) is { } after)
                freed = after - before.Value;

            var estimated = items.Sum(i => i.SizeBytes ?? 0);
            return ServiceResult<CleanupRunResult>.Success(new CleanupRunResult(dryRun, succeeded, failed, 0, freed, estimated,
                dryRun ? items.Select(i => i.Name).ToList() : processed));
        }, cancellationToken);

    internal sealed record CleanupStep(string? Command, string Description, TimeSpan Timeout, bool IsDocker);

    /// <summary>Öğenin komutu; önizlemede simülasyonu olmayan araçlar için Command null döner ve yalnızca açıklama gösterilir.</summary>
    internal static CleanupStep? BuildStep(CleanupItem item, CleanupOptions options, bool dryRun)
    {
        switch (item.Category)
        {
            case CleanupCategory.Containers:
                return Docker(DockerCommands.ContainerAction(item.Target, DockerContainerAction.Remove, force: false), ShortStepTimeout, dryRun);
            case CleanupCategory.Images:
                return Docker(DockerCommands.RemoveImage(item.Target, force: false), LongStepTimeout, dryRun);
            case CleanupCategory.Networks:
                return Docker(DockerCommands.RemoveNetwork(item.Target), ShortStepTimeout, dryRun);
            case CleanupCategory.Volumes:
                return Docker(DockerCommands.RemoveVolume(item.Target), ShortStepTimeout, dryRun);
            case CleanupCategory.BuildCache:
                return Docker(DockerCommands.PruneBuildCache, LongStepTimeout, dryRun);
            case CleanupCategory.PackageCache:
                return new CleanupStep(CleanupCommands.CleanPackages(item.Target, dryRun),
                    dryRun && item.Target == "apt" ? "apt-get -s clean (simülasyon)" : CleanupCommands.DescribePackages(item.Target),
                    LongStepTimeout, IsDocker: false);
            case CleanupCategory.Journal:
                var vacuum = CleanupCommands.VacuumJournal(options.JournalMaxMegabytes);
                return dryRun
                    ? new CleanupStep(CleanupCommands.JournalUsage, $"mevcut kullanım okunuyor; sonra çalıştırılacak: {vacuum}", ShortStepTimeout, IsDocker: false)
                    : new CleanupStep(vacuum, vacuum, LongStepTimeout, IsDocker: false);
            case CleanupCategory.RotatedLogs:
                return FileStep(CleanupCommands.RotatedLogsFind(options.LogDays), dryRun);
            case CleanupCategory.TempFiles:
                return FileStep(CleanupCommands.TempFilesFind(options.TempDays), dryRun);
            case CleanupCategory.Snaps:
                var parts = item.Target.Split('|', 2);
                if (parts.Length != 2)
                    return null;
                var remove = CleanupCommands.RemoveSnapRevision(parts[0], parts[1]);
                return new CleanupStep(dryRun ? null : remove, remove, LongStepTimeout, IsDocker: false);
            default:
                return null;
        }
    }

    private static CleanupStep Docker(string command, TimeSpan timeout, bool dryRun) =>
        new(dryRun ? null : command, command, timeout, IsDocker: true);

    private static CleanupStep FileStep(string find, bool dryRun) =>
        dryRun
            ? new CleanupStep(CleanupCommands.PreviewFiles(find), "silinecek dosyalar listeleniyor (find, silmeden)", ShortStepTimeout, IsDocker: false)
            : new CleanupStep(CleanupCommands.DeleteFiles(find), find + " -delete", LongStepTimeout, IsDocker: false);

    private static string Label(CleanupItem item) =>
        item.Safety == CleanupSafety.Caution ? $"{item.Name} (Dikkat)" : item.Name;

    private static async Task<long?> AvailableBytesAsync(IRemoteCommandExecutor executor, CancellationToken cancellationToken)
    {
        var output = await executor.ExecuteAsync(new RemoteCommand(ServerSystemCommands.Storage, ShortStepTimeout), cancellationToken);
        return ServerSystemParser.IsComplete(output.Stdout) ? CleanupParser.TotalAvailableBytes(output.Stdout) : null;
    }

    private static async Task<RemoteCommandOutput> RunScriptAsync(
        IRemoteCommandExecutor executor, RemoteExecutionContext context, string script, CancellationToken cancellationToken)
    {
        if (context.UseSudo)
        {
            var elevated = await executor.ExecuteAsync(new RemoteCommand(script, ScanTimeout, Elevate: true), cancellationToken);
            if (elevated.TimedOut || ServerSystemParser.IsComplete(elevated.Stdout))
                return elevated;
        }

        return await executor.ExecuteAsync(new RemoteCommand(script, ScanTimeout), cancellationToken);
    }

    private static bool IsDockerMissing(RemoteCommandOutput output) =>
        output.ExitCode == 127
        || (output.Stderr + output.Stdout).Contains("docker: not found", StringComparison.OrdinalIgnoreCase)
        || (output.Stderr + output.Stdout).Contains("command not found", StringComparison.OrdinalIgnoreCase);
}

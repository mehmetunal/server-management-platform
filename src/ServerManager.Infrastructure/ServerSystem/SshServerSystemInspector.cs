using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Infrastructure.ServerSystem;

public sealed class SshServerSystemInspector : IServerSystemInspector
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(90);
    private const int MaxActionOutput = 4000;

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshServerSystemInspector> _logger;

    public SshServerSystemInspector(IRemoteCommandRunner runner, ILogger<SshServerSystemInspector> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<ServiceList>> GetServicesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        ReadAsync(context, ServerSystemCommands.Services, ServerSystemParser.ParseServices, "Servis listesi", elevate: false, cancellationToken);

    public Task<ServiceResult<ProcessList>> GetProcessesAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        ReadAsync(context, ServerSystemCommands.Processes, ServerSystemParser.ParseProcesses, "Process listesi", elevate: false, cancellationToken);

    public Task<ServiceResult<NetworkSnapshot>> GetNetworkAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        ReadAsync(context, ServerSystemCommands.Network, ServerSystemParser.ParseNetwork, "Ağ bilgisi", elevate: context.UseSudo, cancellationToken);

    public Task<ServiceResult<StorageSnapshot>> GetStorageAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        ReadAsync(context, ServerSystemCommands.Storage, ServerSystemParser.ParseStorage, "Disk bilgisi", elevate: false, cancellationToken);

    public Task<ServiceResult<string>> ControlServiceAsync(
        RemoteExecutionContext context, ServiceManagerKind manager, string name, ServiceAction action, CancellationToken cancellationToken = default)
    {
        if (manager == ServiceManagerKind.None || !ServerSystemRules.IsValidServiceName(name))
            return Task.FromResult(ServiceResult<string>.Failure("Geçersiz servis adı."));

        return ActAsync(context, ServerSystemCommands.ControlService(manager, name, action), cancellationToken);
    }

    public Task<ServiceResult<string>> SignalProcessAsync(RemoteExecutionContext context, int pid, ProcessSignal signal, CancellationToken cancellationToken = default)
    {
        if (pid <= 1)
            return Task.FromResult(ServiceResult<string>.Failure("Bu process sonlandırılamaz."));

        return ActAsync(context, ServerSystemCommands.SignalProcess(pid, signal), cancellationToken);
    }

    public Task<ServiceResult<LogSnapshot>> GetLogsAsync(RemoteExecutionContext context, LogRequest request, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var sources = await executor.ExecuteAsync(new RemoteCommand(ServerSystemCommands.LogSources, ReadTimeout), ct);
            if (!ServerSystemParser.IsComplete(sources.Stdout))
                return ServiceResult<LogSnapshot>.Failure("Log kaynakları okunamadı.");

            var (hasJournal, files) = ServerSystemParser.ParseLogSources(sources.Stdout);
            var lines = ServerSystemRules.NormalizeLines(request.Lines);
            string? notice = null;
            var source = request.Source;
            var path = request.Path;

            if (source == LogSource.Journal && !hasJournal)
            {
                path = files.FirstOrDefault(f => f is "/var/log/syslog" or "/var/log/messages");
                source = LogSource.File;
                notice = path is null
                    ? "Bu sunucuda journald yok. Aşağıdaki listeden bir log dosyası seçin."
                    : $"Bu sunucuda journald yok; {path} gösteriliyor.";
            }

            if (source == LogSource.File)
            {
                if (path is null)
                    return ServiceResult<LogSnapshot>.Success(new LogSnapshot(LogSource.File, "Dosya seçilmedi", [], hasJournal, files, notice));
                if (!ServerSystemRules.IsValidLogPath(path))
                    return ServiceResult<LogSnapshot>.Failure("Yalnızca /var/log altındaki dosyalar okunabilir.");
            }

            var command = source == LogSource.Journal
                ? ServerSystemCommands.ReadJournal(lines, request.Unit, request.Priority)
                : ServerSystemCommands.ReadFile(lines, path!);

            var output = await RunWithFallbackAsync(executor, context, command, ReadTimeout, ct);
            if (output.TimedOut)
                return ServiceResult<LogSnapshot>.Failure("Log okuma zaman aşımına uğradı.");

            var text = ServerSystemParser.SplitLines(output.Stdout.Length > 0 ? output.Stdout : output.Stderr);
            var label = source == LogSource.Journal
                ? "journald" + (string.IsNullOrEmpty(request.Unit) ? string.Empty : $" · {request.Unit}")
                : path!;
            return ServiceResult<LogSnapshot>.Success(new LogSnapshot(source, label, ServerSystemRules.ApplyFilter(text, request.Filter), hasJournal, files, notice));
        }, cancellationToken);

    private Task<ServiceResult<T>> ReadAsync<T>(
        RemoteExecutionContext context, string command, Func<string, T> parse, string label, bool elevate, CancellationToken cancellationToken) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            RemoteCommandOutput? output = null;
            if (elevate)
            {
                output = await executor.ExecuteAsync(new RemoteCommand(command, ReadTimeout, Elevate: true), ct);
                if (!ServerSystemParser.IsComplete(output.Stdout))
                    output = null;
            }

            output ??= await executor.ExecuteAsync(new RemoteCommand(command, ReadTimeout), ct);
            if (output.TimedOut)
                return ServiceResult<T>.Failure($"{label} okunurken zaman aşımı oldu.");
            if (!ServerSystemParser.IsComplete(output.Stdout))
                return ServiceResult<T>.Failure($"{label} okunamadı. Sunucunun Linux ve sh kabuğu olduğundan emin olun.");

            try
            {
                return ServiceResult<T>.Success(parse(output.Stdout));
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException)
            {
                _logger.LogWarning(ex, "{Label} çıktısı okunamadı. Target: {Target}", label, context);
                return ServiceResult<T>.Failure($"{label} çıktısı okunamadı.");
            }
        }, cancellationToken);

    private Task<ServiceResult<string>> ActAsync(RemoteExecutionContext context, string command, CancellationToken cancellationToken) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(command, ActionTimeout, Elevate: context.UseSudo), ct);
            var text = TextHelper.Truncate((output.Stdout + output.Stderr).Trim(), MaxActionOutput) ?? string.Empty;
            if (output.TimedOut)
                return ServiceResult<string>.Failure("İşlem zaman aşımına uğradı.");
            if (output.ExitCode != 0)
                return ServiceResult<string>.Failure(text.Length > 0 ? text : $"Komut {output.ExitCode} koduyla bitti.");

            return ServiceResult<string>.Success(text);
        }, cancellationToken);

    private static async Task<RemoteCommandOutput> RunWithFallbackAsync(
        IRemoteCommandExecutor executor, RemoteExecutionContext context, string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (context.UseSudo)
        {
            var elevated = await executor.ExecuteAsync(new RemoteCommand(command, timeout, Elevate: true), cancellationToken);
            if (elevated.ExitCode == 0 || elevated.TimedOut)
                return elevated;
        }

        return await executor.ExecuteAsync(new RemoteCommand(command, timeout), cancellationToken);
    }
}

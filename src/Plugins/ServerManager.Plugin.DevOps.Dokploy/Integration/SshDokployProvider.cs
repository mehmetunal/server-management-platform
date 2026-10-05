using System.Globalization;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Infrastructure.Docker;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;
using ServerManager.Plugin.DevOps.Dokploy.Services;

namespace ServerManager.Plugin.DevOps.Dokploy.Integration;

public sealed class SshDokployProvider : IDokployProvider
{
    private static readonly TimeSpan FactsTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(150);

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshDokployProvider> _logger;

    public SshDokployProvider(IRemoteCommandRunner runner, ILogger<SshDokployProvider> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<DokployHostFacts>> GatherFactsAsync(RemoteExecutionContext context, DokployOptions options, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(5, options.CommandTimeoutSeconds));
            var factsOutput = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.HostFacts, FactsTimeout), ct);
            if (!factsOutput.IsSuccess)
                return ServiceResult<DokployHostFacts>.Failure("Sunucu bilgileri okunamadı.");

            var (values, listening) = DokployOutputParser.ParseKeyValues(factsOutput.Stdout);
            var userId = ParseInt(values.GetValueOrDefault("uid"));
            var curl = values.GetValueOrDefault("has_curl") == "1";
            var dockerInstalled = values.GetValueOrDefault("has_docker") == "1";

            bool? sudoWorks = null;
            string? sudoError = null;
            if (userId is not 0 && context.UseSudo)
            {
                var sudo = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.SudoCheck, timeout, Elevate: true), ct);
                sudoWorks = sudo.IsSuccess;
                sudoError = sudo.IsSuccess ? null : TranslateSudoError(sudo);
            }

            string? dockerVersion = null;
            string? dockerError = null;
            string? swarmState = null;
            var dokployServiceExists = false;
            if (dockerInstalled)
            {
                var info = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.DockerInfo, timeout, Elevate: true), ct);
                if (info.IsSuccess)
                {
                    (dockerVersion, swarmState) = DokployOutputParser.ParseDockerInfo(info.Stdout);
                    if (string.Equals(swarmState, "active", StringComparison.OrdinalIgnoreCase))
                    {
                        var services = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.ListServices, timeout, Elevate: true), ct);
                        dokployServiceExists = services.IsSuccess && DokployOutputParser.ParseServices(services.Stdout).Any(s => s.Name == "dokploy");
                    }
                }
                else
                {
                    dockerError = DockerErrorTranslator.Translate(info);
                }
            }

            var scriptReachable = false;
            string? scriptError = null;
            var registryReachable = false;
            string? registryError = null;
            if (curl)
            {
                var script = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.Reachability(options.InstallScriptUrl, failOnHttpError: true), timeout), ct);
                scriptReachable = script.IsSuccess;
                scriptError = script.IsSuccess ? null : $"Kurulum betiğine erişilemiyor ({options.InstallScriptUrl}): {FirstLine(script.Stderr) ?? $"HTTP {script.Stdout.Trim()}"}";

                var registry = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.Reachability(options.RegistryCheckUrl, failOnHttpError: false), timeout), ct);
                registryReachable = registry.IsSuccess && registry.Stdout.Trim() is { Length: > 0 } code && code != "000";
                registryError = registryReachable ? null : $"Docker Hub'a erişilemiyor: {FirstLine(registry.Stderr) ?? "yanıt yok"}";
            }

            var portsTool = values.GetValueOrDefault("ports_tool");
            return ServiceResult<DokployHostFacts>.Success(new DokployHostFacts
            {
                OsId = NullIfEmpty(values.GetValueOrDefault("os_id")),
                OsVersion = NullIfEmpty(values.GetValueOrDefault("os_version")),
                OsName = NullIfEmpty(values.GetValueOrDefault("os_name")),
                Kernel = NullIfEmpty(values.GetValueOrDefault("kernel")),
                Architecture = NullIfEmpty(values.GetValueOrDefault("arch")),
                UserId = userId,
                ContainerKind = NullIfEmpty(values.GetValueOrDefault("container")),
                MemoryKb = ParseLong(values.GetValueOrDefault("mem_kb")),
                DiskAvailableKb = ParseLong(values.GetValueOrDefault("disk_kb")),
                CurlAvailable = curl,
                BashAvailable = values.GetValueOrDefault("has_bash") == "1",
                UseSudo = context.UseSudo,
                SudoWorks = sudoWorks,
                SudoError = sudoError,
                DockerInstalled = dockerInstalled,
                DockerVersion = dockerVersion,
                DockerError = dockerError,
                SwarmState = swarmState,
                DokployServiceExists = dokployServiceExists,
                ListeningPorts = portsTool is "ss" or "netstat" ? DokployOutputParser.ParseListeningPorts(listening) : null,
                ScriptReachable = scriptReachable,
                ScriptError = scriptError,
                RegistryReachable = registryReachable,
                RegistryError = registryError
            });
        }, cancellationToken);

    public Task<ServiceResult<DokployHostStatusDto>> GetStatusAsync(RemoteExecutionContext context, int port, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var services = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.ListServices, FactsTimeout, Elevate: true), ct);
            var swarmActive = services.IsSuccess;
            if (!services.IsSuccess && !DokployOutputParser.IsSwarmInactive(services.Stderr))
            {
                return ServiceResult<DokployHostStatusDto>.Success(new DokployHostStatusDto
                {
                    DockerAvailable = false,
                    DockerError = DockerErrorTranslator.Translate(services)
                });
            }

            var containers = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.ListContainers, FactsTimeout, Elevate: true), ct);
            var parsedServices = swarmActive ? DokployOutputParser.ParseServices(services.Stdout) : [];
            var health = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.LocalHealth(port), FactsTimeout), ct);

            return ServiceResult<DokployHostStatusDto>.Success(new DokployHostStatusDto
            {
                DockerAvailable = true,
                SwarmActive = swarmActive,
                Services = parsedServices,
                Containers = containers.IsSuccess ? DokployOutputParser.ParseContainers(containers.Stdout) : [],
                ImageTag = DokployVersions.ParseImageTag(parsedServices.FirstOrDefault(s => s.Name == "dokploy")?.Image),
                LocalHealthy = health.IsSuccess && DokployOutputParser.IsHealthyResponse(health.Stdout)
            });
        }, cancellationToken);

    public async Task<bool> IsLocallyHealthyAsync(RemoteExecutionContext context, int port, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            var health = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.LocalHealth(port), FactsTimeout), ct);
            return ServiceResult<bool>.Success(health.IsSuccess && DokployOutputParser.IsHealthyResponse(health.Stdout));
        }, cancellationToken);

        return result is { IsSuccess: true, Data: true };
    }

    public Task<ServiceResult<DokployScriptResult>> RunInstallScriptAsync(
        RemoteExecutionContext context,
        DokployInstallPlan plan,
        IDokployInstallObserver observer,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var path = $"/tmp/sm-dokploy-install-{Guid.NewGuid():N}.sh";
            try
            {
                await observer.OnStageAsync(DokployInstallStage.Downloading, "Kurulum betiği indiriliyor", ct);
                await observer.OnOutputAsync(DokployConsole.Info($"Kurulum betiği indiriliyor: {plan.ScriptUrl}"), ct);

                var download = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.Download(plan.ScriptUrl, path), DownloadTimeout), ct);
                if (!download.IsSuccess)
                    return Failed($"Kurulum betiği indirilemedi: {FirstLine(download.Stderr) ?? (download.TimedOut ? "zaman aşımı" : "bilinmeyen hata")}");

                await executor.ExecuteAsync(new RemoteCommand(DokployCommands.RestrictPermissions(path), FactsTimeout), ct);
                var hash = await executor.ExecuteAsync(new RemoteCommand(DokployCommands.Sha256(path), FactsTimeout), ct);
                var sha256 = hash.IsSuccess ? DokployOutputParser.ParseSha256(hash.Stdout) : null;
                await observer.OnOutputAsync(DokployConsole.Info($"Betik SHA-256: {sha256 ?? "hesaplanamadı"}"), ct);

                var checksumError = DokployOutputParser.VerifySha256(plan.ExpectedSha256, sha256);
                if (checksumError is not null)
                {
                    _logger.LogWarning("Dokploy kurulum betiğinin özeti doğrulanamadı. Target: {Target}, Sha256: {Sha256}", context, sha256);
                    return ServiceResult<DokployScriptResult>.Success(new DokployScriptResult { Sha256 = sha256, ErrorMessage = checksumError });
                }

                var target = plan.Version is null ? "son kararlı sürüm" : plan.Version;
                await observer.OnStageAsync(DokployInstallStage.Installing, "Dokploy kuruluyor", ct);
                await observer.OnOutputAsync(DokployConsole.Info($"Kurulum başlatılıyor ({target}, {(plan.Elevate ? "sudo ile root" : "root")}). Bu işlem birkaç dakika sürebilir."), ct);

                var run = await executor.ExecuteStreamingAsync(
                    new RemoteCommand(DokployCommands.RunScript(path, plan.Version, plan.UseBash), plan.Timeout, Elevate: plan.Elevate),
                    (text, token) => observer.OnOutputAsync(NormalizeNewLines(text), token),
                    ct);

                return ServiceResult<DokployScriptResult>.Success(new DokployScriptResult
                {
                    Sha256 = sha256,
                    ExitCode = run.ExitCode,
                    TimedOut = run.TimedOut,
                    ErrorMessage = run.ExitCode == 1 && SudoFailed(run) ? TranslateSudoError(run) : null
                });
            }
            finally
            {
                try
                {
                    await executor.ExecuteAsync(new RemoteCommand(DokployCommands.Remove(path), FactsTimeout), CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Geçici kurulum betiği silinemedi. Target: {Target}", context);
                }
            }
        }, cancellationToken);

    private static ServiceResult<DokployScriptResult> Failed(string message) =>
        ServiceResult<DokployScriptResult>.Success(new DokployScriptResult { ErrorMessage = message });

    private static string TranslateSudoError(RemoteCommandOutput output)
    {
        if (output.TimedOut)
            return "sudo yanıt vermedi (zaman aşımı).";

        var stderr = output.Stderr;
        if (output.ExitCode == 127 || stderr.Contains("sudo: not found", StringComparison.OrdinalIgnoreCase) || stderr.Contains("sudo: command not found", StringComparison.OrdinalIgnoreCase))
            return "Sunucuda sudo kurulu değil.";
        if (stderr.Contains("a password is required", StringComparison.OrdinalIgnoreCase))
            return "sudo parola istiyor. Sunucu ayarlarına sudo parolasını girin.";
        if (stderr.Contains("incorrect password", StringComparison.OrdinalIgnoreCase) || stderr.Contains("try again", StringComparison.OrdinalIgnoreCase))
            return "Sudo parolası hatalı.";
        if (stderr.Contains("not in the sudoers", StringComparison.OrdinalIgnoreCase))
            return "Kullanıcının sudo yetkisi yok.";
        if (stderr.Contains("not allowed to execute", StringComparison.OrdinalIgnoreCase))
            return "Kullanıcı sudo ile her komutu çalıştıramıyor; kurulum için tam sudo yetkisi gerekir.";

        return FirstLine(stderr) ?? $"sudo başarısız oldu (çıkış kodu {output.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "?"}).";
    }

    private static bool SudoFailed(RemoteCommandOutput output) =>
        output.Stderr.Contains("sudo:", StringComparison.Ordinal) && output.Stdout.Length == 0;

    /// <summary>Terminal (xterm) satır başına dönebilsin diye yalnız LF'ler CRLF'e çevrilir.</summary>
    private static string NormalizeNewLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}

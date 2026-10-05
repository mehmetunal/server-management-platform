using System.Globalization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Docker;

namespace ServerManager.Infrastructure.ManagedServices;

public sealed class SshManagedServiceProvider : IManagedServiceProvider
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StableRunningWindow = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ReadinessWindow = TimeSpan.FromSeconds(90);
    private const int FailureLogTail = 60;

    private readonly IRemoteCommandRunner _runner;
    private readonly ITerminalSessionFactory _terminalFactory;

    public SshManagedServiceProvider(IRemoteCommandRunner runner, ITerminalSessionFactory terminalFactory)
    {
        _runner = runner;
        _terminalFactory = terminalFactory;
    }

    public Task<ServiceResult<ServiceHostProbe>> ProbeAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.Probe(), ShortTimeout, Elevate: true), ct);
            if (!output.IsSuccess)
                return ServiceResult<ServiceHostProbe>.Failure(DockerErrorTranslator.Translate(output));

            return ServiceResult<ServiceHostProbe>.Success(ParseProbe(output.Stdout));
        }, cancellationToken);

    public Task<ServiceResult<ServiceOperationResult>> DeployAsync(
        RemoteExecutionContext context,
        ManagedServicePlan plan,
        IServiceOperationObserver observer,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
            ServiceResult<ServiceOperationResult>.Success(await DeployCoreAsync(executor, plan, observer, ct)), cancellationToken);

    public Task<ServiceResult<ServiceOperationResult>> RemoveAsync(
        RemoteExecutionContext context,
        ManagedServiceRemovalPlan plan,
        IServiceOperationObserver observer,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
            ServiceResult<ServiceOperationResult>.Success(await RemoveCoreAsync(executor, plan, observer, ct)), cancellationToken);

    public Task<ServiceResult<ServiceRuntimeState>> GetRuntimeAsync(RemoteExecutionContext context, string slug, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var container = ManagedServiceNames.ContainerName(slug);
            var inspect = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.Inspect(container), ShortTimeout, Elevate: true), ct);
            var firewall = await executor.ExecuteAsync(new RemoteCommand(ServiceFirewallCommands.Count(slug), ShortTimeout, Elevate: true), ct);
            var rules = firewall.IsSuccess ? ParseFirewallCount(firewall.Stdout) : 0;

            if (!inspect.IsSuccess)
            {
                if (inspect.Stderr.Contains("No such", StringComparison.OrdinalIgnoreCase))
                    return ServiceResult<ServiceRuntimeState>.Success(new ServiceRuntimeState { Exists = false, FirewallRuleCount = rules });

                return ServiceResult<ServiceRuntimeState>.Failure(DockerErrorTranslator.Translate(inspect));
            }

            var state = ParseInspect(inspect.Stdout);
            return ServiceResult<ServiceRuntimeState>.Success(new ServiceRuntimeState
            {
                Exists = true,
                State = state.State,
                Health = state.Health,
                RestartCount = state.RestartCount,
                StartedAt = state.StartedAt,
                Image = state.Image,
                Networks = state.Networks,
                FirewallRuleCount = rules
            });
        }, cancellationToken);

    public async Task<ServiceResult> ApplyFirewallAsync(RemoteExecutionContext context, ServiceFirewallPlan plan, CancellationToken cancellationToken = default)
    {
        var slug = plan.Tag.StartsWith(ManagedServiceNames.Prefix, StringComparison.Ordinal) ? plan.Tag[ManagedServiceNames.Prefix.Length..] : plan.Tag;
        if (!ManagedServiceNames.IsValidSlug(slug))
            return ServiceResult.Failure("Geçersiz servis etiketi.");

        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(
                new RemoteCommand(ServiceFirewallCommands.Apply(plan, slug), ShortTimeout, Elevate: true, StandardInput: ServiceFirewallCommands.ApplyScript(plan)), ct);
            return output.IsSuccess
                ? ServiceResult<bool>.Success(true)
                : ServiceResult<bool>.Failure(TranslateFirewall(output));
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.Message ?? "Güvenlik duvarı kuralları uygulanamadı.");
    }

    public Task<ServiceResult<IReadOnlyList<string>>> ListNetworksAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.ListNetworks(), ShortTimeout, Elevate: true), ct);
            if (!output.IsSuccess)
                return ServiceResult<IReadOnlyList<string>>.Failure(DockerErrorTranslator.Translate(output));

            IReadOnlyList<string> names = output.Stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(ServiceValidation.IsValidNetworkName)
                .Order(StringComparer.Ordinal)
                .ToList();
            return ServiceResult<IReadOnlyList<string>>.Success(names);
        }, cancellationToken);

    public Task<ServiceResult<ITerminalSession>> OpenConsoleAsync(
        RemoteExecutionContext context,
        string container,
        string? consoleCommand,
        int columns,
        int rows,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default) =>
        _terminalFactory.OpenAsync(new TerminalOpenRequest
        {
            Context = context,
            Command = ManagedServiceCommands.Console(container, consoleCommand),
            Elevate = true,
            Columns = columns,
            Rows = rows
        }, sink, cancellationToken);

    // ---------------------------------------------------------------- Kurulum

    private static async Task<ServiceOperationResult> DeployCoreAsync(
        IRemoteCommandExecutor executor,
        ManagedServicePlan plan,
        IServiceOperationObserver observer,
        CancellationToken cancellationToken)
    {
        Func<string, CancellationToken, Task> forward = (text, ct) => observer.OnOutputAsync(ServiceConsole.NormalizeNewLines(text), ct);

        // 1) Docker
        await observer.OnStageAsync(ServiceOperationStage.Docker, "Docker kontrolü", cancellationToken);
        var docker = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.CheckDocker(), ShortTimeout, Elevate: true), cancellationToken);
        if (!docker.IsSuccess)
            return ServiceOperationResult.Failed(TranslateDockerMissing(docker), docker.ExitCode);

        var architecture = Marker(docker.Stdout, "SM_ARCH=");
        await observer.OnOutputAsync(ServiceConsole.Info($"Docker {Marker(docker.Stdout, "SM_DOCKER_VERSION=")} · {architecture}"), cancellationToken);
        if (plan.RequiresX86 && architecture is not ("x86_64" or "amd64"))
        {
            return ServiceOperationResult.Failed(
                $"Bu servis yalnızca x86_64 (amd64) sunucularda çalışır; sunucunun mimarisi {architecture}. ARM sunucularda SQL Server container'ı açılmaz.");
        }

        // 2) Port
        await observer.OnStageAsync(ServiceOperationStage.Ports, "Port çakışma kontrolü", cancellationToken);
        if (plan.Ports.Count == 0)
        {
            await observer.OnOutputAsync(ServiceConsole.Info("Sunucuya port yayınlanmıyor; servis yalnızca Docker ağından erişilebilir."), cancellationToken);
        }
        else
        {
            var ports = await executor.ExecuteAsync(
                new RemoteCommand(ManagedServiceCommands.CheckPorts(plan.ContainerName, plan.Ports.Select(p => p.HostPort)), ShortTimeout, Elevate: true), cancellationToken);
            if (ports.ExitCode == ManagedServiceCommands.PortBusyExitCode)
            {
                var busy = Marker(ports.Stdout, ManagedServiceCommands.PortBusyMarker).Trim();
                return ServiceOperationResult.Failed($"Sunucuda şu portlar kullanımda: {busy}. Ayarlardan başka bir sunucu portu seçin.", ports.ExitCode);
            }

            if (!ports.IsSuccess)
                return ServiceOperationResult.Failed(DockerErrorTranslator.Translate(ports), ports.ExitCode);

            var portText = string.Join(", ", plan.Ports.Select(p => $"{p.BindAddress}:{p.HostPort.ToString(CultureInfo.InvariantCulture)}"));
            await observer.OnOutputAsync(ServiceConsole.Info($"Portlar boş: {portText}"), cancellationToken);
            if (plan.Ports.Any(p => p.BindAddress == ServicePortBindings.AnyAddress) && !plan.Firewall.HasRules)
            {
                await observer.OnOutputAsync(ServiceConsole.Warning(
                    "Port dışarıya açık ve IP kısıtlaması yok: Docker'ın yayınladığı portlar UFW kurallarını atlar, port internete açıktır."), cancellationToken);
            }
        }

        // 3) İmaj
        await observer.OnStageAsync(ServiceOperationStage.Pull, $"İmaj indiriliyor: {plan.ImageReference}", cancellationToken);
        var pullCommand = ManagedServiceCommands.Pull(plan.ImageReference);
        await observer.OnOutputAsync(ServiceConsole.Info("$ " + pullCommand), cancellationToken);
        var pull = await executor.ExecuteStreamingAsync(new RemoteCommand(pullCommand, plan.PullTimeout, Elevate: true), forward, cancellationToken);
        if (!pull.IsSuccess)
            return ServiceOperationResult.Failed(pull.TimedOut ? "İmaj indirme zaman aşımına uğradı." : DockerErrorTranslator.Translate(pull), pull.ExitCode);

        // 4) Ağ ve volume
        await observer.OnStageAsync(ServiceOperationStage.Network, "Ağ ve volume hazırlanıyor", cancellationToken);
        var required = plan.Networks.Except(plan.ManagedNetworks, StringComparer.Ordinal).ToList();
        var networks = await executor.ExecuteAsync(
            new RemoteCommand(ManagedServiceCommands.EnsureNetworks(plan.ManagedNetworks, required), plan.CommandTimeout, Elevate: true), cancellationToken);
        if (networks.ExitCode == ManagedServiceCommands.NetworkMissingExitCode)
        {
            var missing = Marker(networks.Stderr, ManagedServiceCommands.NetworkMissingMarker).Trim('\'');
            return ServiceOperationResult.Failed($"\"{missing}\" Docker ağı sunucuda bulunamadı; ağı Docker sekmesinden oluşturun veya ayarlardan kaldırın.", networks.ExitCode);
        }

        if (!networks.IsSuccess)
            return ServiceOperationResult.Failed(DockerErrorTranslator.Translate(networks), networks.ExitCode);

        await observer.OnOutputAsync(ServiceConsole.Info("Ağlar: " + string.Join(", ", plan.Networks)), cancellationToken);

        if (plan.DataPath is not null)
        {
            var hostPath = plan.VolumeMode == ManagedServiceVolumeMode.HostPath ? plan.HostDataPath : null;
            var volumeCommand = hostPath is null
                ? ManagedServiceCommands.EnsureVolume(plan.Slug)
                : ManagedServiceCommands.EnsureHostDirectory(plan.Slug, hostPath, plan.DataOwner);
            var volume = await executor.ExecuteAsync(new RemoteCommand(volumeCommand, plan.CommandTimeout, Elevate: true), cancellationToken);
            if (volume.ExitCode == ManagedServiceCommands.NotManagedExitCode)
                return ServiceOperationResult.Failed($"{hostPath} sembolik bağlantı; veri klasörü olarak gerçek bir klasör seçin.", volume.ExitCode);
            if (!volume.IsSuccess)
                return ServiceOperationResult.Failed("Veri alanı hazırlanamadı: " + DockerErrorTranslator.Translate(volume), volume.ExitCode);

            await observer.OnOutputAsync(ServiceConsole.Info($"Veri: {hostPath ?? plan.VolumeName} → {plan.DataPath}"), cancellationToken);
        }

        // 5) Container
        await observer.OnStageAsync(ServiceOperationStage.Start, "Container oluşturuluyor", cancellationToken);
        var env = await executor.ExecuteAsync(
            new RemoteCommand(ManagedServiceCommands.WriteEnvironmentFile(plan.Slug), ShortTimeout, Elevate: true, StandardInput: plan.EnvironmentFile), cancellationToken);
        if (!env.IsSuccess)
            return ServiceOperationResult.Failed("Ortam dosyası yazılamadı: " + DockerErrorTranslator.Translate(env), env.ExitCode);

        await observer.OnOutputAsync(ServiceConsole.Info($"Ortam dosyası yazıldı: {ManagedServiceNames.EnvironmentFile(plan.Slug)} (0600, yalnızca root okuyabilir)"), cancellationToken);

        if (plan.ReplaceExisting)
        {
            var removeCommand = ManagedServiceCommands.RemoveContainer(plan.ContainerName);
            await observer.OnOutputAsync(ServiceConsole.Info("$ " + removeCommand), cancellationToken);
            var remove = await executor.ExecuteAsync(new RemoteCommand(removeCommand, plan.CommandTimeout, Elevate: true), cancellationToken);
            if (!remove.IsSuccess && !remove.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
                return ServiceOperationResult.Failed("Eski container kaldırılamadı: " + DockerErrorTranslator.Translate(remove), remove.ExitCode);
        }

        var createCommand = ManagedServiceCommands.Create(plan);
        await observer.OnOutputAsync(ServiceConsole.Info("$ " + createCommand), cancellationToken);
        var create = await executor.ExecuteAsync(new RemoteCommand(createCommand, plan.CommandTimeout, Elevate: true), cancellationToken);
        if (!create.IsSuccess)
        {
            var reason = create.Stderr.Contains("is already in use", StringComparison.OrdinalIgnoreCase)
                ? $"{plan.ContainerName} adında bir container zaten var (panel dışında oluşturulmuş olabilir). Docker sekmesinden kaldırıp tekrar deneyin."
                : "Container oluşturulamadı: " + DockerErrorTranslator.Translate(create);
            return ServiceOperationResult.Failed(reason, create.ExitCode);
        }

        foreach (var network in plan.Networks.Skip(1))
        {
            var connect = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.ConnectNetwork(network, plan.ContainerName), plan.CommandTimeout, Elevate: true), cancellationToken);
            if (!connect.IsSuccess)
                return await FailWithLogsAsync(executor, plan, observer, $"\"{network}\" ağına bağlanılamadı: {DockerErrorTranslator.Translate(connect)}", connect.ExitCode, cancellationToken);
        }

        // Port dışarıya açılmadan önce kısıtlama yazılır; böylece container açıkken korumasız bir an olmaz.
        var firewall = await executor.ExecuteAsync(
            new RemoteCommand(ServiceFirewallCommands.Apply(plan.Firewall, plan.Slug), ShortTimeout, Elevate: true, StandardInput: ServiceFirewallCommands.ApplyScript(plan.Firewall)), cancellationToken);
        if (!firewall.IsSuccess)
        {
            await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.RemoveContainer(plan.ContainerName), ShortTimeout, Elevate: true), CancellationToken.None);
            return ServiceOperationResult.Failed(TranslateFirewall(firewall) + " Port açık kalmasın diye container başlatılmadı.", firewall.ExitCode);
        }

        if (plan.Firewall.HasRules)
        {
            await observer.OnOutputAsync(ServiceConsole.Info(
                $"Güvenlik duvarı (DOCKER-USER): {string.Join(", ", plan.Firewall.Ports)} portları yalnızca {string.Join(", ", plan.Firewall.AllowedSources)} için açık."), cancellationToken);
        }

        var start = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.Start(plan.ContainerName), plan.CommandTimeout, Elevate: true), cancellationToken);
        if (!start.IsSuccess)
        {
            var reason = start.Stderr.Contains("address already in use", StringComparison.OrdinalIgnoreCase) || start.Stderr.Contains("port is already allocated", StringComparison.OrdinalIgnoreCase)
                ? "Container başlatılamadı: sunucu portu kullanımda. Ayarlardan başka bir port seçin."
                : "Container başlatılamadı: " + DockerErrorTranslator.Translate(start);
            return await FailWithLogsAsync(executor, plan, observer, reason, start.ExitCode, cancellationToken);
        }

        await observer.OnOutputAsync(ServiceConsole.Success($"{plan.ContainerName} başlatıldı."), cancellationToken);

        // 6) Sağlık
        await observer.OnStageAsync(ServiceOperationStage.Health, "Sağlık kontrolü bekleniyor", cancellationToken);
        var health = await WaitHealthyAsync(executor, plan, observer, cancellationToken);
        if (health is not null)
            return await FailWithLogsAsync(executor, plan, observer, health, null, cancellationToken);

        // 7) Bağlantı testi
        await observer.OnStageAsync(ServiceOperationStage.Test, "Bağlantı testi", cancellationToken);
        if (plan.ReadinessCommand is null)
        {
            await observer.OnOutputAsync(ServiceConsole.Info("Bu servis için ayrıca bağlantı testi yok; container çalışıyor."), cancellationToken);
            return ServiceOperationResult.Success();
        }

        var test = await RunReadinessAsync(executor, plan, cancellationToken);
        if (test is not null)
            return await FailWithLogsAsync(executor, plan, observer, test, null, cancellationToken);

        await observer.OnOutputAsync(ServiceConsole.Success("Kimlik bilgileriyle bağlantı başarılı."), cancellationToken);
        return ServiceOperationResult.Success();
    }

    private static async Task<string?> WaitHealthyAsync(IRemoteCommandExecutor executor, ManagedServicePlan plan, IServiceOperationObserver observer, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + plan.HealthTimeout;
        DateTimeOffset? runningSince = null;
        var lastReported = string.Empty;
        var initialRestarts = -1;

        while (true)
        {
            var inspect = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.Inspect(plan.ContainerName), ShortTimeout, Elevate: true), cancellationToken);
            if (!inspect.IsSuccess)
                return "Container durumu okunamadı: " + DockerErrorTranslator.Translate(inspect);

            var state = ParseInspect(inspect.Stdout);
            if (initialRestarts < 0)
                initialRestarts = state.RestartCount;

            var label = string.IsNullOrEmpty(state.Health) ? state.State : $"{state.State} / {state.Health}";
            if (!string.Equals(label, lastReported, StringComparison.Ordinal))
            {
                lastReported = label;
                await observer.OnOutputAsync(ServiceConsole.Info($"Durum: {label}"), cancellationToken);
            }

            switch (state.State)
            {
                case "exited" or "dead":
                    return "Container başladıktan sonra durdu; aşağıdaki container loglarını inceleyin.";
                case "restarting" when state.RestartCount - initialRestarts >= 2:
                    return "Container sürekli yeniden başlıyor; aşağıdaki container loglarını inceleyin.";
            }

            if (state.Health == "healthy")
                return null;

            if (state.Health == "unhealthy")
                return "Sağlık kontrolü başarısız (unhealthy); aşağıdaki container loglarını inceleyin.";

            if (state.State == "running" && string.IsNullOrEmpty(state.Health))
            {
                runningSince ??= DateTimeOffset.UtcNow;
                if (DateTimeOffset.UtcNow - runningSince >= StableRunningWindow && state.RestartCount == initialRestarts)
                    return null;
            }
            else
            {
                runningSince = null;
            }

            if (DateTimeOffset.UtcNow >= deadline)
                return $"Servis {(int)plan.HealthTimeout.TotalSeconds} saniye içinde hazır olmadı (son durum: {label}).";

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private static async Task<string?> RunReadinessAsync(IRemoteCommandExecutor executor, ManagedServicePlan plan, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + ReadinessWindow;
        RemoteCommandOutput? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await executor.ExecuteAsync(
                new RemoteCommand(ManagedServiceCommands.Exec(plan.ContainerName, plan.ReadinessCommand!), ShortTimeout, Elevate: true), cancellationToken);
            if (last.IsSuccess)
                return null;

            await Task.Delay(PollInterval, cancellationToken);
        }

        var detail = last is null ? string.Empty : FirstLine(last.Stderr.Length > 0 ? last.Stderr : last.Stdout);
        return "Bağlantı testi başarısız oldu" + (detail.Length > 0 ? $": {detail}" : ".");
    }

    private static async Task<ServiceOperationResult> FailWithLogsAsync(
        IRemoteCommandExecutor executor,
        ManagedServicePlan plan,
        IServiceOperationObserver observer,
        string reason,
        int? exitCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var logs = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.Logs(plan.ContainerName, FailureLogTail), ShortTimeout, Elevate: true), cancellationToken);
            var text = (logs.Stdout + logs.Stderr).Trim();
            if (text.Length > 0)
            {
                await observer.OnOutputAsync(ServiceConsole.Warning($"Container'ın son {FailureLogTail} log satırı:"), cancellationToken);
                await observer.OnOutputAsync(ServiceConsole.NormalizeNewLines(text + "\n"), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log okunamazsa asıl hata yine bildirilir.
        }

        return ServiceOperationResult.Failed(reason, exitCode);
    }

    // ---------------------------------------------------------------- Kaldırma

    private static async Task<ServiceOperationResult> RemoveCoreAsync(
        IRemoteCommandExecutor executor,
        ManagedServiceRemovalPlan plan,
        IServiceOperationObserver observer,
        CancellationToken cancellationToken)
    {
        if (!ManagedServiceNames.IsValidSlug(plan.Slug))
            return ServiceOperationResult.Failed("Servis kısa adı geçersiz.");

        await observer.OnStageAsync(ServiceOperationStage.Docker, "Docker kontrolü", cancellationToken);
        var docker = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.CheckDocker(), ShortTimeout, Elevate: true), cancellationToken);
        if (!docker.IsSuccess)
            return ServiceOperationResult.Failed(TranslateDockerMissing(docker), docker.ExitCode);

        await observer.OnStageAsync(ServiceOperationStage.Stop, "Container durduruluyor ve siliniyor", cancellationToken);
        var removeCommand = ManagedServiceCommands.RemoveContainer(plan.ContainerName);
        await observer.OnOutputAsync(ServiceConsole.Info("$ " + removeCommand), cancellationToken);
        var remove = await executor.ExecuteAsync(new RemoteCommand(removeCommand, plan.CommandTimeout, Elevate: true), cancellationToken);
        if (!remove.IsSuccess && !remove.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
            return ServiceOperationResult.Failed("Container kaldırılamadı: " + DockerErrorTranslator.Translate(remove), remove.ExitCode);

        await observer.OnStageAsync(ServiceOperationStage.Cleanup, "Güvenlik duvarı ve veri temizliği", cancellationToken);
        var firewall = await executor.ExecuteAsync(new RemoteCommand(ServiceFirewallCommands.Remove(plan.Slug), ShortTimeout, Elevate: true), cancellationToken);
        if (!firewall.IsSuccess)
            await observer.OnOutputAsync(ServiceConsole.Warning("Güvenlik duvarı kuralları silinemedi: " + TranslateFirewall(firewall)), cancellationToken);
        else
            await observer.OnOutputAsync(ServiceConsole.Info("Güvenlik duvarı kuralları kaldırıldı."), cancellationToken);

        var data = await executor.ExecuteAsync(new RemoteCommand(ManagedServiceCommands.RemoveData(plan), plan.CommandTimeout, Elevate: true), cancellationToken);
        if (data.ExitCode == ManagedServiceCommands.NotManagedExitCode)
        {
            return ServiceOperationResult.Failed(
                $"{plan.HostDataPath} klasörü kurulumda panel tarafından oluşturulmadığı için silinmedi. Klasörü elle silin veya veriyi koruyarak kaldırın.",
                data.ExitCode);
        }

        if (!data.IsSuccess)
            return ServiceOperationResult.Failed("Veri silinemedi: " + DockerErrorTranslator.Translate(data), data.ExitCode);

        var dataMessage = !plan.RemoveData
            ? $"Veri korundu ({(plan.VolumeMode == ManagedServiceVolumeMode.HostPath ? plan.HostDataPath : plan.VolumeName)})."
            : "Veri silindi.";
        await observer.OnOutputAsync(ServiceConsole.Info(dataMessage), cancellationToken);
        return ServiceOperationResult.Success();
    }

    // ---------------------------------------------------------------- Ayrıştırma

    internal sealed record InspectState(string State, string? Health, int RestartCount, DateTime? StartedAt, string? Image, IReadOnlyList<string> Networks);

    internal static InspectState ParseInspect(string stdout)
    {
        var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        var parts = line.Split('|');
        string Part(int index) => index < parts.Length ? parts[index].Trim() : string.Empty;

        DateTime? started = DateTime.TryParse(Part(3), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value)
                            && value.Year > 1
            ? value
            : null;
        return new InspectState(
            Part(0),
            Part(1).Length == 0 ? null : Part(1),
            int.TryParse(Part(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var restarts) ? restarts : 0,
            started,
            Part(4).Length == 0 ? null : Part(4),
            Part(5).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    internal static ServiceHostProbe ParseProbe(string stdout) => new()
    {
        Architecture = NullIfEmpty(Marker(stdout, "SM_ARCH=")),
        DockerInstalled = Marker(stdout, "SM_DOCKER=") == "installed",
        DockerVersion = NullIfEmpty(Marker(stdout, "SM_DOCKER_VERSION=")),
        DockerRunning = Marker(stdout, "SM_DOCKER_VERSION=").Length > 0,
        DokployDetected = Marker(stdout, "SM_DOKPLOY=") == "1",
        DokkuDetected = Marker(stdout, "SM_DOKKU=") == "1"
    };

    internal static int ParseFirewallCount(string stdout) =>
        int.TryParse(Marker(stdout, "SM_FW="), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;

    private static string TranslateDockerMissing(RemoteCommandOutput output) =>
        output.ExitCode == ManagedServiceCommands.DockerMissingExitCode
            ? "Sunucuda Docker kurulu değil. Sunucunun Docker sekmesinden durumu kontrol edin ve Docker'ı kurun (https://docs.docker.com/engine/install/)."
            : DockerErrorTranslator.Translate(output);

    private static string TranslateFirewall(RemoteCommandOutput output)
    {
        if (output.ExitCode == ServiceFirewallCommands.FirewallUnavailableExitCode)
            return "IP kısıtlaması uygulanamadı: sunucuda iptables veya Docker'ın DOCKER-USER zinciri bulunamadı.";

        var line = FirstLine(output.Stderr);
        return output.TimedOut
            ? "Güvenlik duvarı komutu zaman aşımına uğradı."
            : "Güvenlik duvarı kuralları uygulanamadı" + (line.Length > 0 ? $": {line}" : ".");
    }

    private static string Marker(string text, string prefix)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..];
        }

        return string.Empty;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return line.Length <= 200 ? line : line[..200];
    }
}

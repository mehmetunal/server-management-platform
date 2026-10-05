using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Enums;

namespace ServerManager.Infrastructure.Deployments;

public sealed class SshDeploymentProvider : IDeploymentProvider
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(30);

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshDeploymentProvider> _logger;

    public SshDeploymentProvider(IRemoteCommandRunner runner, ILogger<SshDeploymentProvider> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<IReadOnlyList<string>>> ListBranchesAsync(
        RemoteExecutionContext context,
        GitSource source,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(
                new RemoteCommand(DeploymentCommands.ListBranches(source), timeout, StandardInput: DeploymentCommands.TokenInput(source)), ct);
            if (!output.IsSuccess)
            {
                _logger.LogInformation("Git dalları listelenemedi. Repository: {Repository}, ExitCode: {ExitCode}", source.RepositoryUrl, output.ExitCode);
                return ServiceResult<IReadOnlyList<string>>.Failure(DeploymentErrorTranslator.TranslateGit(output, timeout));
            }

            return ServiceResult<IReadOnlyList<string>>.Success(DeploymentOutputParser.ParseBranches(output.Stdout));
        }, cancellationToken);

    public Task<ServiceResult<ProxyStatusDto>> GetProxyStatusAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ProxyStatus(), ShortTimeout, Elevate: true), ct);
            if (!output.IsSuccess)
                return ServiceResult<ProxyStatusDto>.Failure(DeploymentErrorTranslator.TranslateDocker(output, "Vekil durumu", ShortTimeout));

            return ServiceResult<ProxyStatusDto>.Success(ParseProxyStatus(output.Stdout));
        }, cancellationToken);

    public async Task<ServiceResult> InstallProxyAsync(RemoteExecutionContext context, string acmeEmail, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.InstallProxy(acmeEmail), timeout, Elevate: true), ct);
            if (output.ExitCode == DeploymentCommands.ProxyBusyExitCode)
                return ServiceResult<string>.Failure("80 veya 443 portu dolu. Vekil bu portları dinler; önce mevcut servisi durdurun. " + FirstLine(output.Stderr));

            if (!output.IsSuccess)
                return ServiceResult<string>.Failure(DeploymentErrorTranslator.TranslateDocker(output, "Vekil kurulumu", timeout));

            var marker = Marker(output.Stdout);
            var message = marker switch
            {
                "running" => "Vekil zaten çalışıyor.",
                "started" => "Durmuş olan vekil başlatıldı.",
                _ => "Vekil kuruldu. 80 ve 443 portu sm-traefik tarafından dinleniyor."
            };
            return ServiceResult<string>.Success(message);
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success(result.Data) : ServiceResult.Failure(result.Message ?? "Vekil kurulamadı.", result.ErrorType);
    }

    public async Task<ServiceResult> ApplyRoutingAsync(RemoteExecutionContext context, DeploymentPlan plan, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(context, (executor, ct) => ApplyRoutingCoreAsync(executor, plan, timeout, ct), cancellationToken);
        return result.IsSuccess ? ServiceResult.Success(result.Data) : ServiceResult.Failure(result.Message ?? "Yönlendirme uygulanamadı.", result.ErrorType);
    }

    public async Task<ServiceResult> RemoveDeploymentAsync(
        RemoteExecutionContext context, DeploymentPlan plan, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!DeployPaths.TryValidate(plan.DeployPath, out var pathError))
            return ServiceResult.Failure(pathError ?? "Deploy klasörü silinemez.");

        if (!DeploymentNames.IsValidSlug(plan.Slug))
            return ServiceResult.Failure("Proje kısa adı geçersiz.");

        var result = await _runner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.RemoveProject(plan), timeout, Elevate: true), ct);
            if (output.ExitCode == DeploymentCommands.NotManagedExitCode)
            {
                return ServiceResult<string>.Failure(
                    $"{plan.DeployPath} klasörü panel tarafından oluşturulmamış ({DeploymentCommands.ManagedMarkerFile} işareti yok); sunucuda hiçbir şey silinmedi. " +
                    "Klasörü elle temizleyin veya projeyi sunucudaki dosyaları silmeden kaldırın.");
            }

            return output.IsSuccess
                ? ServiceResult<string>.Success("Sunucudaki uygulama silindi.")
                : ServiceResult<string>.Failure(DeploymentErrorTranslator.TranslateDocker(output, "Uygulamanın silinmesi", timeout));
        }, cancellationToken);

        return result.IsSuccess
            ? ServiceResult.Success(result.Data)
            : ServiceResult.Failure(result.Message ?? "Sunucudaki uygulama silinemedi.", result.ErrorType);
    }

    public Task<ServiceResult<DeploymentRunResult>> DeployAsync(
        RemoteExecutionContext context,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, (executor, ct) => RunAsync(executor, plan, observer, ct), cancellationToken);

    public Task<ServiceResult<DeploymentRunResult>> RestartAsync(
        RemoteExecutionContext context,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, (executor, ct) => RestartCoreAsync(executor, plan, observer, ct), cancellationToken);

    /// <summary>
    /// Kaynak kod ve build olmadan: .env ve compose override yeniden yazılır, container'lar mevcut imajla yeniden oluşturulur.
    /// Compose'da <c>up -d --no-build</c> yapılandırması (ortam değişkenleri dahil) değişen servisleri yeniden oluşturur.
    /// </summary>
    private static async Task<ServiceResult<DeploymentRunResult>> RestartCoreAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        Func<string, CancellationToken, Task> forward = (text, ct) => observer.OnOutputAsync(DeploymentConsole.NormalizeNewLines(text), ct);
        if (plan.BuildType == DeploymentBuildType.Commands)
            return Failed("Komutla dağıtılan projede yeniden başlatma yapılmaz; deploy edin.", null);

        var ready = plan.BuildType == DeploymentBuildType.Dockerfile
            ? await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ImageExists(plan.Slug), ShortTimeout, Elevate: true), cancellationToken)
            : await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ComposeFileExists(plan), ShortTimeout, Elevate: true), cancellationToken);
        if (!ready.IsSuccess)
            return Failed("Sunucuda dağıtılmış bir uygulama bulunamadı; önce deploy edin.", ready.ExitCode, plan.Commit);

        var env = await WriteEnvironmentAsync(executor, plan, observer, cancellationToken);
        if (env is not null)
            return Failed(env.Value.Reason, env.Value.ExitCode, plan.Commit);

        var overrideFile = await SyncComposeOverrideAsync(executor, plan, observer, cancellationToken);
        if (overrideFile is not null)
            return Failed(overrideFile.Value.Reason, overrideFile.Value.ExitCode, plan.Commit);

        await observer.OnStageAsync(DeploymentStage.Deploying, "Container'lar yeniden oluşturuluyor", cancellationToken);
        if (plan.Routes.Count > 0)
        {
            var files = await SyncProxyFilesAsync(executor, plan, observer, cancellationToken);
            if (files is not null)
                return Failed(files.Value.Reason, files.Value.ExitCode, plan.Commit);
        }

        var run = plan.BuildType == DeploymentBuildType.DockerCompose
            ? await RunDockerAsync(executor, DeploymentCommands.ComposeUp(plan, noBuild: true), "Yeniden başlatma", plan.DeployTimeout, observer, forward, cancellationToken)
            : await ReplaceContainerAsync(executor, plan, "latest", observer, forward, cancellationToken);
        if (run is not null)
            return Failed(run.Value.Reason, run.Value.ExitCode, plan.Commit);

        return ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult { Succeeded = true, ExitCode = 0, CommitSha = plan.Commit });
    }

    private static async Task<ServiceResult<DeploymentRunResult>> RunAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        Func<string, CancellationToken, Task> forward = (text, ct) => observer.OnOutputAsync(DeploymentConsole.NormalizeNewLines(text), ct);

        if (plan.PreferExistingImage && plan.BuildType == DeploymentBuildType.Dockerfile && GitRefs.IsValidCommit(plan.Commit))
        {
            var exists = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.CommitImageExists(plan.Slug, plan.Commit!), ShortTimeout, Elevate: true), cancellationToken);
            if (RollbackPlanner.Choose(plan.BuildType, exists.IsSuccess) == RollbackStrategy.RunExistingImage)
                return await RunExistingImageAsync(executor, plan, observer, forward, cancellationToken);

            await observer.OnOutputAsync(DeploymentConsole.Info($"{GitRefs.ShortSha(plan.Commit)} imajı sunucuda yok; commit yeniden çekilip build edilecek."), cancellationToken);
        }

        await observer.OnStageAsync(DeploymentStage.Source, "Kaynak kod alınıyor", cancellationToken);
        var prepare = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.PrepareWorkspace(plan.DeployPath), ShortTimeout), cancellationToken);
        if (!prepare.IsSuccess)
        {
            return Failed(prepare.ExitCode == DeploymentCommands.WorkspaceNotEmptyExitCode
                ? $"{plan.DeployPath} klasörü boş değil ve bir git deposu değil. Mevcut dosyalar silinmez; boş bir klasör seçin veya klasörü elle temizleyin."
                : DeploymentErrorTranslator.TranslateGit(prepare, ShortTimeout), prepare.ExitCode);
        }

        if (prepare.Stdout.Contains(DeploymentCommands.WorkspaceInitializedMarker, StringComparison.Ordinal))
            await observer.OnOutputAsync(DeploymentConsole.Info($"{plan.DeployPath} klasöründe yeni git deposu oluşturuldu."), cancellationToken);

        var reference = plan.Commit is null ? $"{plan.Branch} dalı" : $"commit {GitRefs.ShortSha(plan.Commit)}";
        await observer.OnOutputAsync(DeploymentConsole.Info($"git fetch {plan.Source.RepositoryUrl} ({reference})"), cancellationToken);
        var fetch = await executor.ExecuteStreamingAsync(
            new RemoteCommand(DeploymentCommands.FetchSource(plan), plan.GitTimeout, StandardInput: DeploymentCommands.TokenInput(plan.Source)),
            forward,
            cancellationToken);
        if (!fetch.IsSuccess)
            return Failed(DeploymentErrorTranslator.TranslateGit(fetch, plan.GitTimeout), fetch.ExitCode);

        var read = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ReadCommit(plan.DeployPath), ShortTimeout), cancellationToken);
        var commit = read.IsSuccess ? DeploymentOutputParser.ParseCommit(read.Stdout) : null;
        if (commit is null)
            return Failed("Çekilen commit bilgisi okunamadı.", read.ExitCode);

        var (sha, author, subject) = commit;
        if (plan.Commit is not null && !string.Equals(sha, plan.Commit, StringComparison.Ordinal))
            return Failed($"İstenen commit ({GitRefs.ShortSha(plan.Commit)}) yerine {GitRefs.ShortSha(sha)} alındı.", null, sha, author, subject);

        await observer.OnCommitAsync(commit, cancellationToken);
        var by = author is null ? string.Empty : $" ({author})";
        await observer.OnOutputAsync(DeploymentConsole.Success($"Commit {GitRefs.ShortSha(sha)}: {subject}{by}"), cancellationToken);

        var env = await WriteEnvironmentAsync(executor, plan, observer, cancellationToken);
        if (env is not null)
            return Failed(env.Value.Reason, env.Value.ExitCode, sha, author, subject);

        // Override build'den önce yazılır: compose build/up aynı dosya listesini kullanır.
        var overrideFile = await SyncComposeOverrideAsync(executor, plan, observer, cancellationToken);
        if (overrideFile is not null)
            return Failed(overrideFile.Value.Reason, overrideFile.Value.ExitCode, sha, author, subject);

        await observer.OnStageAsync(DeploymentStage.Building, "Build", cancellationToken);
        var build = await BuildAsync(executor, plan, sha, observer, forward, cancellationToken);
        if (build is not null)
            return Failed(build.Value.Reason, build.Value.ExitCode, sha, author, subject);

        await observer.OnStageAsync(DeploymentStage.Deploying, "Deploy", cancellationToken);
        var deploy = await DeployAsync(executor, plan, sha, observer, forward, cancellationToken);
        if (deploy is not null)
            return Failed(deploy.Value.Reason, deploy.Value.ExitCode, sha, author, subject);

        if (plan.BuildType == DeploymentBuildType.Dockerfile)
            await PruneImagesAsync(executor, plan, sha, observer, cancellationToken);

        return ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult
        {
            Succeeded = true,
            ExitCode = 0,
            CommitSha = sha,
            CommitAuthor = author,
            CommitMessage = subject
        });
    }

    private static async Task<(string Reason, int? ExitCode)?> BuildAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        string sha,
        IDeploymentObserver observer,
        Func<string, CancellationToken, Task> forward,
        CancellationToken cancellationToken)
    {
        switch (plan.BuildType)
        {
            case DeploymentBuildType.DockerCompose:
                return await RunDockerAsync(executor, DeploymentCommands.ComposeBuild(plan), "Build", plan.BuildTimeout, observer, forward, cancellationToken);
            case DeploymentBuildType.Dockerfile:
                return await RunDockerAsync(executor, DeploymentCommands.DockerBuild(plan, sha), "Build", plan.BuildTimeout, observer, forward, cancellationToken);
            default:
                if (string.IsNullOrWhiteSpace(plan.BuildCommand))
                {
                    await observer.OnOutputAsync(DeploymentConsole.Info("Build komutu tanımlı değil; adım atlandı."), cancellationToken);
                    return null;
                }

                return await RunUserCommandAsync(executor, plan, plan.BuildCommand, "Build", plan.BuildTimeout, observer, forward, cancellationToken);
        }
    }

    private static async Task<(string Reason, int? ExitCode)?> DeployAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        string sha,
        IDeploymentObserver observer,
        Func<string, CancellationToken, Task> forward,
        CancellationToken cancellationToken)
    {
        if (plan.Routes.Count > 0 && plan.BuildType is DeploymentBuildType.DockerCompose or DeploymentBuildType.Dockerfile)
        {
            var synced = await SyncProxyFilesAsync(executor, plan, observer, cancellationToken);
            if (synced is not null)
                return synced;
        }

        switch (plan.BuildType)
        {
            case DeploymentBuildType.DockerCompose:
                return await RunDockerAsync(executor, DeploymentCommands.ComposeUp(plan), "Deploy", plan.DeployTimeout, observer, forward, cancellationToken);
            case DeploymentBuildType.Dockerfile:
                return await ReplaceContainerAsync(executor, plan, sha, observer, forward, cancellationToken);
            default:
                return await RunUserCommandAsync(executor, plan, plan.DeployCommand ?? string.Empty, "Deploy", plan.DeployTimeout, observer, forward, cancellationToken);
        }
    }

    /// <summary>Dockerfile projesinde eski container'ı kaldırıp <paramref name="imageTag"/> (kısa SHA veya latest) imajıyla yenisini çalıştırır.</summary>
    private static async Task<(string Reason, int? ExitCode)?> ReplaceContainerAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        string imageTag,
        IDeploymentObserver observer,
        Func<string, CancellationToken, Task> forward,
        CancellationToken cancellationToken)
    {
        var removeCommand = DeploymentCommands.DockerRemoveContainer(plan.Slug);
        await observer.OnOutputAsync(DeploymentConsole.Info("$ " + removeCommand), cancellationToken);
        var remove = await executor.ExecuteAsync(new RemoteCommand(removeCommand, ShortTimeout, Elevate: true), cancellationToken);
        if (!remove.IsSuccess && !remove.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
            return (DeploymentErrorTranslator.TranslateDocker(remove, "Eski container'ın kaldırılması", ShortTimeout), remove.ExitCode);

        return await RunDockerAsync(executor, DeploymentCommands.DockerRun(plan, imageTag), "Deploy", plan.DeployTimeout, observer, forward, cancellationToken);
    }

    /// <summary>Geri dönüş: commit imajı sunucuda duruyor; kaynak kod ve build atlanır, imaj latest yapılıp çalıştırılır.</summary>
    private static async Task<ServiceResult<DeploymentRunResult>> RunExistingImageAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        Func<string, CancellationToken, Task> forward,
        CancellationToken cancellationToken)
    {
        var sha = plan.Commit!;
        await observer.OnStageAsync(DeploymentStage.Source, "Kayıtlı imaj kullanılıyor", cancellationToken);
        await observer.OnOutputAsync(DeploymentConsole.Success($"{DeploymentNames.ImageName(plan.Slug)}:{GitRefs.ShortSha(sha)} imajı sunucuda mevcut; kaynak kod çekilmeyecek ve build yapılmayacak."), cancellationToken);
        await observer.OnCommitAsync(new DeploymentCommit(sha, null, null), cancellationToken);

        var env = await WriteEnvironmentAsync(executor, plan, observer, cancellationToken);
        if (env is not null)
            return Failed(env.Value.Reason, env.Value.ExitCode, sha);

        await observer.OnStageAsync(DeploymentStage.Deploying, "Deploy", cancellationToken);
        var tag = await RunDockerAsync(executor, DeploymentCommands.TagCommitImageAsLatest(plan.Slug, sha), "İmaj etiketleme", ShortTimeout, observer, forward, cancellationToken);
        if (tag is not null)
            return Failed(tag.Value.Reason, tag.Value.ExitCode, sha);

        if (plan.Routes.Count > 0)
        {
            var files = await SyncProxyFilesAsync(executor, plan, observer, cancellationToken);
            if (files is not null)
                return Failed(files.Value.Reason, files.Value.ExitCode, sha);
        }

        var run = await ReplaceContainerAsync(executor, plan, sha, observer, forward, cancellationToken);
        if (run is not null)
            return Failed(run.Value.Reason, run.Value.ExitCode, sha);

        await PruneImagesAsync(executor, plan, sha, observer, cancellationToken);
        return ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult { Succeeded = true, ExitCode = 0, CommitSha = sha });
    }

    /// <summary>Eski commit imajlarını siler; hata deployment'ı başarısız yapmaz.</summary>
    private static async Task PruneImagesAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        string currentSha,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        if (plan.KeepImageCount <= 0)
            return;

        var prune = await executor.ExecuteAsync(
            new RemoteCommand(DeploymentCommands.PruneCommitImages(plan.Slug, currentSha, plan.KeepImageCount), ShortTimeout, Elevate: true), cancellationToken);
        if (!prune.IsSuccess)
        {
            await observer.OnOutputAsync(DeploymentConsole.Info("Eski imajlar temizlenemedi; deployment etkilenmedi."), cancellationToken);
            return;
        }

        var removed = prune.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (removed.Length > 0)
            await observer.OnOutputAsync(DeploymentConsole.Info($"Son {plan.KeepImageCount} imaj saklandı; silinen eski imajlar: {string.Join(", ", removed)}"), cancellationToken);
    }

    private static async Task<(string Reason, int? ExitCode)?> WriteEnvironmentAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        if (plan.Environment is null)
            return null;

        var env = await executor.ExecuteAsync(
            new RemoteCommand(DeploymentCommands.WriteEnvironmentFile(plan.DeployPath), ShortTimeout, StandardInput: plan.Environment), cancellationToken);
        if (!env.IsSuccess)
            return (".env dosyası yazılamadı: " + DeploymentErrorTranslator.TranslateGit(env, ShortTimeout), env.ExitCode);

        await observer.OnOutputAsync(DeploymentConsole.Info(".env dosyası yazıldı (yalnızca SSH kullanıcısı okuyabilir)."), cancellationToken);
        return null;
    }

    private static async Task<(string Reason, int? ExitCode)?> RunDockerAsync(
        IRemoteCommandExecutor executor,
        string command,
        string step,
        TimeSpan timeout,
        IDeploymentObserver observer,
        Func<string, CancellationToken, Task> forward,
        CancellationToken cancellationToken)
    {
        await observer.OnOutputAsync(DeploymentConsole.Info("$ " + command), cancellationToken);
        var output = await executor.ExecuteStreamingAsync(new RemoteCommand(command, timeout, Elevate: true), forward, cancellationToken);
        return output.IsSuccess ? null : (DeploymentErrorTranslator.TranslateDocker(output, step, timeout), output.ExitCode);
    }

    private static async Task<(string Reason, int? ExitCode)?> RunUserCommandAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        string command,
        string step,
        TimeSpan timeout,
        IDeploymentObserver observer,
        Func<string, CancellationToken, Task> forward,
        CancellationToken cancellationToken)
    {
        var sudo = plan.UseSudoForCommands ? " (sudo)" : string.Empty;
        await observer.OnOutputAsync(DeploymentConsole.Info($"{step} komutu {plan.DeployPath} klasöründe çalışıyor{sudo}"), cancellationToken);
        var output = await executor.ExecuteStreamingAsync(
            new RemoteCommand(DeploymentCommands.RunUserCommand(plan.DeployPath, command), timeout, Elevate: plan.UseSudoForCommands),
            forward,
            cancellationToken);
        return output.IsSuccess ? null : (DeploymentErrorTranslator.TranslateCommand(output, step, timeout), output.ExitCode);
    }

    /// <summary>Traefik dinamik dosyalarını (özel sertifikalar) eşitler.</summary>
    private static async Task<(string Reason, int? ExitCode)?> SyncProxyFilesAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        var missing = plan.BuildType == DeploymentBuildType.DockerCompose ? TraefikRoutes.FindRouteWithoutService(plan.Routes) : null;
        if (missing is not null)
            return (MissingServiceMessage(missing), null);

        await observer.OnOutputAsync(DeploymentConsole.Info("Vekil dosyaları güncelleniyor."), cancellationToken);
        var files = await executor.ExecuteAsync(
            new RemoteCommand(DeploymentCommands.SyncProxyFiles(plan.Slug), ShortTimeout, Elevate: true, StandardInput: TraefikRoutes.CertificateInput(plan.Slug, plan.Routes)),
            cancellationToken);
        return files.IsSuccess ? null : (DeploymentErrorTranslator.TranslateDocker(files, "Vekil dosyaları", ShortTimeout), files.ExitCode);
    }

    /// <summary>
    /// Compose projesinde panel override'ını yazar veya (ortam değişkeni ve domain yoksa) siler. Ortam değişkeni varsa
    /// <c>docker compose config --services</c> ile servisler okunur ve .env hepsine <c>env_file</c> olarak eklenir.
    /// </summary>
    private static async Task<(string Reason, int? ExitCode)?> SyncComposeOverrideAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        if (plan.BuildType != DeploymentBuildType.DockerCompose)
            return null;

        var missing = TraefikRoutes.FindRouteWithoutService(plan.Routes);
        if (missing is not null)
            return (MissingServiceMessage(missing), null);

        var hasEnvironment = plan.Environment is not null;
        if (!ComposeOverride.IsNeeded(hasEnvironment, plan.Routes, plan.JoinServicesNetwork))
        {
            var remove = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.RemoveOverride(plan.DeployPath), ShortTimeout, Elevate: true), cancellationToken);
            return remove.IsSuccess ? null : (DeploymentErrorTranslator.TranslateDocker(remove, "Compose override dosyası", ShortTimeout), remove.ExitCode);
        }

        IReadOnlyList<string> services = [];
        if (hasEnvironment || plan.JoinServicesNetwork)
        {
            var list = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ComposeServices(plan), ShortTimeout, Elevate: true), cancellationToken);
            if (!list.IsSuccess)
                return ("Compose servisleri okunamadı: " + DeploymentErrorTranslator.TranslateDocker(list, "Compose yapılandırması", ShortTimeout), list.ExitCode);

            services = ComposeOverride.ParseServices(list.Stdout);
            if (hasEnvironment)
            {
                await observer.OnOutputAsync(DeploymentConsole.Info(services.Count == 0
                    ? "Compose dosyasında servis bulunamadı; ortam değişkenleri aktarılmadı."
                    : $"Ortam değişkenleri (.env) tüm servislere aktarılıyor: {string.Join(", ", services)}"), cancellationToken);
            }
        }

        if (plan.JoinServicesNetwork)
        {
            var network = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.EnsureServicesNetwork(), ShortTimeout, Elevate: true), cancellationToken);
            if (!network.IsSuccess)
                return (DeploymentErrorTranslator.TranslateDocker(network, "Servis ağı (" + DomainNames.ServicesNetwork + ")", ShortTimeout), network.ExitCode);

            await observer.OnOutputAsync(DeploymentConsole.Info(
                $"Projeye bağlı servisler var; servisler {DomainNames.ServicesNetwork} ağına katılıyor: {string.Join(", ", services)}"), cancellationToken);
        }

        string content;
        try
        {
            content = ComposeOverride.Build(
                hasEnvironment ? services : [],
                DeploymentCommands.EnvironmentFilePath(plan.DeployPath),
                plan.Routes,
                plan.JoinServicesNetwork ? services : null);
        }
        catch (InvalidOperationException ex)
        {
            return (ex.Message, null);
        }

        var write = await executor.ExecuteAsync(
            new RemoteCommand(DeploymentCommands.WriteOverride(plan.DeployPath), ShortTimeout, Elevate: true, StandardInput: content),
            cancellationToken);
        return write.IsSuccess ? null : (DeploymentErrorTranslator.TranslateDocker(write, "Compose override dosyası", ShortTimeout), write.ExitCode);
    }

    private static string MissingServiceMessage(DeploymentRoute route) =>
        $"{route.Host} domaininde Compose servis adı yok; domaini düzenleyip servis adını girin (ör. web).";

    private static async Task<ServiceResult<string>> ApplyRoutingCoreAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (plan.BuildType == DeploymentBuildType.Commands)
            return ServiceResult<string>.Failure("Komutla dağıtılan projede yönlendirme uygulanmaz.");

        if (plan.Routes.Count > 0)
        {
            var status = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ProxyStatus(), ShortTimeout, Elevate: true), cancellationToken);
            if (!status.IsSuccess)
                return ServiceResult<string>.Failure(DeploymentErrorTranslator.TranslateDocker(status, "Vekil durumu", ShortTimeout));

            if (Marker(status.Stdout) != "running")
                return ServiceResult<string>.Failure(DomainNames.ProxyMissingMessage);
        }

        var ready = plan.BuildType == DeploymentBuildType.Dockerfile
            ? await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ImageExists(plan.Slug), ShortTimeout, Elevate: true), cancellationToken)
            : await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ComposeFileExists(plan), ShortTimeout, Elevate: true), cancellationToken);
        if (!ready.IsSuccess)
            return ServiceResult<string>.Failure(DomainNames.NotReadyMessage);

        var observer = NullDeploymentObserver.Instance;
        var synced = await SyncProxyFilesAsync(executor, plan, observer, cancellationToken)
                     ?? await SyncComposeOverrideAsync(executor, plan, observer, cancellationToken);
        if (synced is not null)
            return ServiceResult<string>.Failure(synced.Value.Reason);

        if (plan.BuildType == DeploymentBuildType.Dockerfile)
        {
            var remove = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.DockerRemoveContainer(plan.Slug), ShortTimeout, Elevate: true), cancellationToken);
            if (!remove.IsSuccess && !remove.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<string>.Failure(DeploymentErrorTranslator.TranslateDocker(remove, "Eski container'ın kaldırılması", ShortTimeout));

            var run = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.DockerRun(plan, "latest"), timeout, Elevate: true), cancellationToken);
            return run.IsSuccess
                ? ServiceResult<string>.Success("Yönlendirme uygulandı.")
                : ServiceResult<string>.Failure(DeploymentErrorTranslator.TranslateDocker(run, "Yönlendirme", timeout));
        }

        var up = await executor.ExecuteAsync(new RemoteCommand(DeploymentCommands.ComposeUp(plan, noBuild: true), timeout, Elevate: true), cancellationToken);
        return up.IsSuccess
            ? ServiceResult<string>.Success("Yönlendirme uygulandı.")
            : ServiceResult<string>.Failure(DeploymentErrorTranslator.TranslateDocker(up, "Yönlendirme", timeout));
    }

    private static ProxyStatusDto ParseProxyStatus(string stdout)
    {
        var marker = Marker(stdout);
        return marker switch
        {
            "running" => new ProxyStatusDto { Installed = true, Running = true, Message = "Vekil çalışıyor." },
            "missing" => new ProxyStatusDto { Installed = false, Running = false, Message = "Vekil kurulu değil." },
            _ => new ProxyStatusDto { Installed = true, Running = false, Message = "Vekil durmuş. Kurulum düğmesi onu yeniden başlatır." }
        };
    }

    private static string Marker(string stdout)
    {
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            const string prefix = "SM_PROXY=";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..];
        }

        return string.Empty;
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return line.Length <= 200 ? line : line[..200];
    }

    private static ServiceResult<DeploymentRunResult> Failed(string reason, int? exitCode, string? sha = null, string? author = null, string? subject = null) =>
        ServiceResult<DeploymentRunResult>.Success(new DeploymentRunResult
        {
            Succeeded = false,
            FailureReason = reason,
            ExitCode = exitCode,
            CommitSha = sha,
            CommitAuthor = author,
            CommitMessage = subject
        });
}

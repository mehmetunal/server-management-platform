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

    public Task<ServiceResult<DeploymentRunResult>> DeployAsync(
        RemoteExecutionContext context,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, (executor, ct) => RunAsync(executor, plan, observer, ct), cancellationToken);

    private static async Task<ServiceResult<DeploymentRunResult>> RunAsync(
        IRemoteCommandExecutor executor,
        DeploymentPlan plan,
        IDeploymentObserver observer,
        CancellationToken cancellationToken)
    {
        Func<string, CancellationToken, Task> forward = (text, ct) => observer.OnOutputAsync(DeploymentConsole.NormalizeNewLines(text), ct);

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

        if (plan.Environment is not null)
        {
            var env = await executor.ExecuteAsync(
                new RemoteCommand(DeploymentCommands.WriteEnvironmentFile(plan.DeployPath), ShortTimeout, StandardInput: plan.Environment), cancellationToken);
            if (!env.IsSuccess)
                return Failed(".env dosyası yazılamadı: " + DeploymentErrorTranslator.TranslateGit(env, ShortTimeout), env.ExitCode, sha, author, subject);

            await observer.OnOutputAsync(DeploymentConsole.Info(".env dosyası yazıldı (yalnızca SSH kullanıcısı okuyabilir)."), cancellationToken);
        }

        await observer.OnStageAsync(DeploymentStage.Building, "Build", cancellationToken);
        var build = await BuildAsync(executor, plan, sha, observer, forward, cancellationToken);
        if (build is not null)
            return Failed(build.Value.Reason, build.Value.ExitCode, sha, author, subject);

        await observer.OnStageAsync(DeploymentStage.Deploying, "Deploy", cancellationToken);
        var deploy = await DeployAsync(executor, plan, sha, observer, forward, cancellationToken);
        if (deploy is not null)
            return Failed(deploy.Value.Reason, deploy.Value.ExitCode, sha, author, subject);

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
        switch (plan.BuildType)
        {
            case DeploymentBuildType.DockerCompose:
                return await RunDockerAsync(executor, DeploymentCommands.ComposeUp(plan), "Deploy", plan.DeployTimeout, observer, forward, cancellationToken);
            case DeploymentBuildType.Dockerfile:
                var removeCommand = DeploymentCommands.DockerRemoveContainer(plan.Slug);
                await observer.OnOutputAsync(DeploymentConsole.Info("$ " + removeCommand), cancellationToken);
                var remove = await executor.ExecuteAsync(new RemoteCommand(removeCommand, ShortTimeout, Elevate: true), cancellationToken);
                if (!remove.IsSuccess && !remove.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
                    return (DeploymentErrorTranslator.TranslateDocker(remove, "Eski container'ın kaldırılması", ShortTimeout), remove.ExitCode);

                return await RunDockerAsync(executor, DeploymentCommands.DockerRun(plan, sha), "Deploy", plan.DeployTimeout, observer, forward, cancellationToken);
            default:
                return await RunUserCommandAsync(executor, plan, plan.DeployCommand ?? string.Empty, "Deploy", plan.DeployTimeout, observer, forward, cancellationToken);
        }
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

using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class DeploymentService : IDeploymentService
{
    public const string InterruptedReason = "Uygulama kapandığı için deployment yarıda kesildi; sunucudaki durumu kontrol edip yeniden deploy edin.";
    public const string CancelledReason = "Deployment kullanıcı tarafından iptal edildi.";
    private const string RunningConflictMessage = "Bu proje için süren bir deployment var; bitmesini bekleyin veya iptal edin.";

    private readonly IDeploymentRepository _repository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDeploymentProvider _provider;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<StartDeploymentDto> _startValidator;
    private readonly IGitIntegrationRegistry _gitIntegrations;
    private readonly DeploymentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(
        IDeploymentRepository repository,
        IServerConnectionProvider connectionProvider,
        IDeploymentProvider provider,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        IValidator<StartDeploymentDto> startValidator,
        IGitIntegrationRegistry gitIntegrations,
        IOptions<DeploymentOptions> options,
        TimeProvider timeProvider,
        ILogger<DeploymentService> logger)
    {
        _repository = repository;
        _connectionProvider = connectionProvider;
        _provider = provider;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _startValidator = startValidator;
        _gitIntegrations = gitIntegrations;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<DeploymentListItemDto>> SearchAsync(DeploymentFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _repository.SearchDeploymentsAsync(filter, cancellationToken);
        return page.Map(d => d.ToListItemDto());
    }

    public async Task<ServiceResult<DeploymentDetailsDto>> GetAsync(Guid id, bool includeLog, CancellationToken cancellationToken = default)
    {
        var deployment = await _repository.GetDeploymentAsync(id, cancellationToken);
        if (deployment is null)
            return ServiceResult<DeploymentDetailsDto>.NotFound("Deployment kaydı bulunamadı.");

        var project = await _repository.GetProjectAsync(deployment.ProjectId, cancellationToken);
        return ServiceResult<DeploymentDetailsDto>.Success(deployment.ToDetailsDto(project, includeLog));
    }

    public async Task<ServiceResult<Guid>> BeginAsync(Guid projectId, StartDeploymentDto dto, DeploymentActor actor, CancellationToken cancellationToken = default)
    {
        var validation = await _startValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<Guid>.NotFound("Proje bulunamadı.");

        return await BeginCoreAsync(project, GitRefs.NormalizeCommit(dto.CommitSha), null, actor, cancellationToken);
    }

    public async Task<ServiceResult<Guid>> BeginRedeployAsync(Guid deploymentId, DeploymentActor actor, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetDeploymentAsync(deploymentId, cancellationToken);
        if (source is null)
            return ServiceResult<Guid>.NotFound("Deployment kaydı bulunamadı.");

        if (string.IsNullOrEmpty(source.CommitSha))
            return ServiceResult<Guid>.Failure("Bu deployment'ın commit bilgisi yok (kaynak kod alınamadan bitmiş); projeden yeni deployment başlatın.");

        var project = await _repository.GetProjectAsync(source.ProjectId, cancellationToken);
        if (project is null)
            return ServiceResult<Guid>.Failure("Proje silindiği için yeniden deploy edilemez.", ServiceErrorType.NotFound);

        return await BeginCoreAsync(project, source.CommitSha, source.Id, actor, cancellationToken);
    }

    public async Task<ServiceResult> RunAsync(Guid deploymentId, DeploymentActor actor, IDeploymentObserver observer, DeploymentCancellation cancellation)
    {
        var deployment = await _repository.GetDeploymentAsync(deploymentId, CancellationToken.None);
        if (deployment is null || deployment.Status != DeploymentStatus.Started)
            return ServiceResult.NotFound("Başlamayı bekleyen deployment bulunamadı.");

        var recorder = new DeploymentRecorder(
            observer,
            Math.Max(64, _options.MaxStoredLogKilobytes) * 1024,
            (log, ct) => _repository.UpdateDeploymentLogAsync(deploymentId, log, ct),
            (stage, ct) => OnStageChangedAsync(deployment, stage, ct),
            (commit, ct) => OnCommitResolvedAsync(deployment, commit, ct),
            _timeProvider);

        try
        {
            var result = await ExecuteAsync(deployment, recorder, cancellation.Token);
            if (cancellation.Token.IsCancellationRequested)
                return await CompleteCancelledAsync(deployment, recorder, cancellation);

            await CompleteAsync(deployment, recorder, result, actor);
            return result.Succeeded ? ServiceResult.Success("Deployment tamamlandı.") : ServiceResult.Failure(result.FailureReason ?? "Deployment başarısız oldu.");
        }
        catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
        {
            return await CompleteCancelledAsync(deployment, recorder, cancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment beklenmeyen hata ile bitti. DeploymentId: {DeploymentId}", deploymentId);
            var result = new DeploymentRunResult { Succeeded = false, FailureReason = "Beklenmeyen bir hata oluştu; ayrıntılar uygulama loglarında." };
            await CompleteAsync(deployment, recorder, result, actor);
            return ServiceResult.Failure(result.FailureReason);
        }
    }

    public Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default) =>
        _repository.InterruptRunningDeploymentsAsync(UtcNow, InterruptedReason, cancellationToken);

    private async Task<ServiceResult<Guid>> BeginCoreAsync(DeploymentProject project, string? commit, Guid? sourceDeploymentId, DeploymentActor actor, CancellationToken cancellationToken)
    {
        if (await _repository.GetRunningDeploymentAsync(project.Id, cancellationToken) is not null)
            return ServiceResult<Guid>.Failure(RunningConflictMessage, ServiceErrorType.Conflict);

        var deployment = new Deployment
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ServerId = project.ServerId,
            ServerName = project.Server?.Name ?? string.Empty,
            BuildType = project.BuildType,
            Branch = project.Branch,
            RequestedCommit = commit,
            SourceDeploymentId = sourceDeploymentId,
            Status = DeploymentStatus.Started,
            UserId = actor.UserId,
            UserName = actor.UserName,
            IpAddress = actor.IpAddress,
            StartedAt = UtcNow
        };
        await _repository.AddDeploymentAsync(deployment, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var target = commit is null ? $"Dal: {project.Branch}" : $"Commit: {GitRefs.ShortSha(commit)}";
        var details = sourceDeploymentId is null ? target : $"{target} | Yeniden deploy (kaynak: {sourceDeploymentId})";
        await AuditAsync(AuditActions.DeploymentStart, deployment, details, true, actor, cancellationToken);

        _logger.LogInformation("Deployment başlatıldı. DeploymentId: {DeploymentId}, ProjectId: {ProjectId}", deployment.Id, project.Id);
        return ServiceResult<Guid>.Success(deployment.Id, "Deployment başlatıldı.");
    }

    private async Task<DeploymentRunResult> ExecuteAsync(Deployment deployment, DeploymentRecorder recorder, CancellationToken cancellationToken)
    {
        await recorder.OnStageAsync(DeploymentStage.Preparing, "Hazırlık", cancellationToken);

        var project = await _repository.GetProjectAsync(deployment.ProjectId, cancellationToken);
        if (project is null)
            return Failed("Proje bulunamadı veya silinmiş.");

        var target = deployment.RequestedCommit is null ? $"{project.Branch} dalının son commit'i" : $"commit {GitRefs.ShortSha(deployment.RequestedCommit)}";
        await recorder.InfoAsync($"{project.Name} → {deployment.ServerName} ({target})", cancellationToken);

        var connection = await _connectionProvider.GetAsync(project.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return Failed(connection.Message ?? "Sunucuya bağlanılamadı.");

        string? token;
        string? environment;
        try
        {
            token = project.EncryptedAccessToken is null ? null : _secretProtector.Unprotect(project.EncryptedAccessToken);
            environment = project.EncryptedEnvironment is null ? null : _secretProtector.Unprotect(project.EncryptedEnvironment);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Proje gizli bilgileri çözülemedi. ProjectId: {ProjectId}", project.Id);
            return Failed("Erişim anahtarı veya ortam değişkenleri çözülemedi. Master key değişmiş olabilir; projeyi düzenleyip yeniden girin.");
        }

        var username = GitRepositoryUrls.TokenUsername(project.GitProvider, project.GitUsername);
        if (project.GitIntegration is not null)
        {
            var integration = _gitIntegrations.Find(project.GitIntegration);
            if (integration is null)
                return Failed($"Projenin Git entegrasyonu ({project.GitIntegration}) kurulu veya etkin değil; Eklentiler sayfasından etkinleştirin.");

            await recorder.InfoAsync($"{integration.DisplayName}: {project.GitRepository} için erişim anahtarı alınıyor", cancellationToken);
            var access = await integration.CreateAccessTokenAsync(project.GitSourceId ?? string.Empty, project.GitRepository ?? string.Empty, cancellationToken);
            if (!access.IsSuccess)
                return Failed(access.Message ?? "Git entegrasyonundan erişim anahtarı alınamadı.");

            username = access.Data!.Username;
            token = access.Data.Token;
        }

        PortMappings.TryParse(project.PortMappings, out var ports, out _);
        var plan = new DeploymentPlan
        {
            Slug = project.Slug,
            Source = new GitSource
            {
                RepositoryUrl = project.RepositoryUrl,
                Username = username,
                AccessToken = token
            },
            Branch = project.Branch,
            Commit = deployment.RequestedCommit,
            DeployPath = project.DeployPath,
            BuildType = project.BuildType,
            ComposeFile = project.ComposeFile ?? "docker-compose.yml",
            DockerfilePath = project.DockerfilePath ?? "Dockerfile",
            PortMappings = ports,
            BuildCommand = project.BuildCommand,
            DeployCommand = project.DeployCommand,
            UseSudoForCommands = project.UseSudoForCommands,
            Environment = environment,
            GitTimeout = TimeSpan.FromSeconds(Math.Max(30, _options.GitTimeoutSeconds)),
            BuildTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.BuildTimeoutMinutes)),
            DeployTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.DeployTimeoutMinutes))
        };

        var run = await _provider.DeployAsync(connection.Data!.Context, plan, recorder, cancellationToken);
        return run.IsSuccess ? run.Data! : Failed(run.Message ?? "Deployment çalıştırılamadı.");
    }

    private async Task OnStageChangedAsync(Deployment deployment, DeploymentStage stage, CancellationToken cancellationToken)
    {
        switch (stage)
        {
            case DeploymentStage.Building:
                deployment.Status = DeploymentStatus.Building;
                deployment.BuildStartedAt = UtcNow;
                break;
            case DeploymentStage.Deploying:
                deployment.Status = DeploymentStatus.Deploying;
                deployment.DeployStartedAt = UtcNow;
                break;
            default:
                return;
        }

        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task OnCommitResolvedAsync(Deployment deployment, DeploymentCommit commit, CancellationToken cancellationToken)
    {
        ApplyCommit(deployment, commit.Sha, commit.Author, commit.Subject);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyCommit(Deployment deployment, string? sha, string? author, string? subject)
    {
        deployment.CommitSha = sha ?? deployment.CommitSha;
        deployment.CommitMessage = TextHelper.Truncate(subject, 500) ?? deployment.CommitMessage;
        deployment.CommitAuthor = TextHelper.Truncate(author, 256) ?? deployment.CommitAuthor;
    }

    private async Task<ServiceResult> CompleteCancelledAsync(Deployment deployment, DeploymentRecorder recorder, DeploymentCancellation cancellation)
    {
        var byUser = cancellation.IsCancelledByUser && !cancellation.IsShutdown;
        var status = byUser ? DeploymentStatus.Cancelled : DeploymentStatus.Interrupted;
        var reason = byUser ? CancelledReason : InterruptedReason;

        await WriteFinalAsync(deployment, recorder, DeploymentStage.Cancelled, DeploymentConsole.Error(reason));

        deployment.Status = status;
        deployment.FailureReason = reason;
        deployment.CancelledBy = byUser ? cancellation.CancelledBy?.UserName : null;
        deployment.CompletedAt = UtcNow;
        deployment.Log = recorder.Log.ToString();
        await _repository.SaveChangesAsync(CancellationToken.None);

        var actor = byUser && cancellation.CancelledBy is not null
            ? cancellation.CancelledBy
            : new DeploymentActor(deployment.UserId, deployment.UserName, deployment.IpAddress);
        await AuditAsync(AuditActions.DeploymentCancel, deployment, reason, false, actor, CancellationToken.None);
        return ServiceResult.Failure(reason);
    }

    private async Task CompleteAsync(Deployment deployment, DeploymentRecorder recorder, DeploymentRunResult result, DeploymentActor actor)
    {
        var message = result.Succeeded ? "Deployment başarıyla tamamlandı." : result.FailureReason ?? "Deployment başarısız oldu.";
        await WriteFinalAsync(
            deployment,
            recorder,
            result.Succeeded ? DeploymentStage.Completed : DeploymentStage.Failed,
            result.Succeeded ? DeploymentConsole.Success(message) : DeploymentConsole.Error(message));

        deployment.Status = result.Succeeded ? DeploymentStatus.Succeeded : DeploymentStatus.Failed;
        deployment.FailureReason = result.Succeeded ? null : TextHelper.Truncate(message, 1000);
        deployment.ExitCode = result.ExitCode;
        ApplyCommit(deployment, result.CommitSha, result.CommitAuthor, result.CommitMessage);
        deployment.CompletedAt = UtcNow;
        deployment.Log = recorder.Log.ToString();
        await _repository.SaveChangesAsync(CancellationToken.None);

        var commit = deployment.CommitSha is null ? string.Empty : $" | Commit: {GitRefs.ShortSha(deployment.CommitSha)}";
        await AuditAsync(AuditActions.DeploymentComplete, deployment, $"Sonuç: {message}{commit}", result.Succeeded, actor, CancellationToken.None);
    }

    private async Task WriteFinalAsync(Deployment deployment, DeploymentRecorder recorder, DeploymentStage stage, string line)
    {
        try
        {
            await recorder.OnOutputAsync(line, CancellationToken.None);
            await recorder.OnStageAsync(stage, stage switch
            {
                DeploymentStage.Completed => "Tamamlandı",
                DeploymentStage.Cancelled => "İptal edildi",
                _ => "Başarısız"
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Deployment sonucu izleyiciye iletilemedi. DeploymentId: {DeploymentId}", deployment.Id);
        }
    }

    private static DeploymentRunResult Failed(string reason) => new() { Succeeded = false, FailureReason = reason };

    private Task AuditAsync(string action, Deployment deployment, string details, bool isSuccess, DeploymentActor actor, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Project,
            deployment.ProjectId.ToString(),
            deployment.ProjectName,
            details,
            isSuccess,
            UserNameOverride: actor.UserName,
            UserIdOverride: actor.UserId,
            IpAddressOverride: actor.IpAddress), cancellationToken);
}

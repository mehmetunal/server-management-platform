using System.IO.Pipelines;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Mappings;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class BackupRunService : IBackupRunService
{
    public const string InterruptedReason = "Uygulama kapandığı için işlem yarıda kesildi.";
    public const string CancelledReason = "İşlem kullanıcı tarafından iptal edildi.";
    public const string RetentionActor = "Saklama politikası";

    private const string RunNotFoundMessage = "Yedek kaydı bulunamadı.";
    private const string ArtifactUnavailableMessage = "Bu yedeğin dosyası yok (başarısız, silinmiş veya saklama süresi dolmuş).";
    private const long PipePauseThreshold = 8 * 1024 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(15);

    private readonly IBackupRepository _repository;
    private readonly IServerRepository _serverRepository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IBackupSourceRunner _sourceRunner;
    private readonly IBackupStorageService _storageService;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<BackupRestoreDto> _restoreValidator;
    private readonly TimeProvider _timeProvider;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupRunService> _logger;

    public BackupRunService(
        IBackupRepository repository,
        IServerRepository serverRepository,
        IServerConnectionProvider connectionProvider,
        IBackupSourceRunner sourceRunner,
        IBackupStorageService storageService,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        IValidator<BackupRestoreDto> restoreValidator,
        TimeProvider timeProvider,
        IOptions<BackupOptions> options,
        ILogger<BackupRunService> logger)
    {
        _repository = repository;
        _serverRepository = serverRepository;
        _connectionProvider = connectionProvider;
        _sourceRunner = sourceRunner;
        _storageService = storageService;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _restoreValidator = restoreValidator;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<BackupRunListItemDto>> SearchAsync(BackupRunFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = Paging.NormalizePage(filter.Page);
        var pageSize = Paging.NormalizePageSize(filter.PageSize);
        var (items, total) = await _repository.SearchRunsAsync(filter, page, pageSize, cancellationToken);
        return new PagedResult<BackupRunListItemDto>(items.Select(r => r.ToListItemDto()).ToList(), total, page, pageSize);
    }

    public async Task<ServiceResult<BackupRunDetailsDto>> GetAsync(Guid id, bool includeLog, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetRunAsync(id, cancellationToken);
        return run is null
            ? ServiceResult<BackupRunDetailsDto>.NotFound(RunNotFoundMessage)
            : ServiceResult<BackupRunDetailsDto>.Success(run.ToDetailsDto(includeLog));
    }

    public async Task<IReadOnlyList<BackupRunListItemDto>> GetRecentAsync(Guid jobId, int count, CancellationToken cancellationToken = default)
    {
        var runs = await _repository.GetRecentRunsAsync(jobId, Math.Clamp(count, 1, 100), cancellationToken);
        return runs.Select(r => r.ToListItemDto()).ToList();
    }

    public async Task<ServiceResult<Guid>> BeginBackupAsync(Guid jobId, BackupTrigger trigger, BackupActor actor, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(jobId, cancellationToken);
        if (job is null)
            return ServiceResult<Guid>.NotFound("Yedekleme işi bulunamadı.");

        if (await _repository.HasRunningBackupAsync(job.Id, cancellationToken))
            return ServiceResult<Guid>.Failure("Bu iş için süren bir yedekleme var; bitmesini bekleyin veya iptal edin.", ServiceErrorType.Conflict);

        if (job.EncryptionEnabled && string.IsNullOrEmpty(job.EncryptedPassphrase))
            return ServiceResult<Guid>.Failure("Şifreleme açık ama parola kayıtlı değil; işi düzenleyip parola girin.");

        var run = new BackupRun
        {
            Operation = BackupOperation.Backup,
            Trigger = trigger,
            Status = BackupRunStatus.Running,
            JobId = job.Id,
            JobName = job.Name,
            SourceType = job.SourceType,
            ServerId = job.ServerId,
            ServerName = job.Server?.Name ?? string.Empty,
            StorageId = job.StorageId,
            StorageName = job.Storage?.Name ?? string.Empty,
            IsEncrypted = job.EncryptionEnabled,
            EncryptedPassphrase = job.EncryptionEnabled ? job.EncryptedPassphrase : null,
            UserName = actor.UserName,
            StartedAt = UtcNow
        };
        run.ObjectKey = BackupNames.ObjectKey(job.Id, run.Id, run.StartedAt, job.SourceType, run.IsEncrypted);
        run.FileName = BackupNames.FileName(job.Name, run.StartedAt, job.SourceType, run.IsEncrypted);

        await _repository.AddRunAsync(run, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var how = trigger == BackupTrigger.Scheduled ? "Zamanlanmış" : "Elle";
        await AuditAsync(AuditActions.BackupStart, run, $"{how} | {BackupSourceDescriber.Describe(job)} → {run.StorageName}", true, actor, cancellationToken);
        _logger.LogInformation("Yedekleme başlatıldı. RunId: {RunId}, JobId: {JobId}, Trigger: {Trigger}", run.Id, job.Id, trigger);
        return ServiceResult<Guid>.Success(run.Id, "Yedekleme başlatıldı.");
    }

    public async Task<ServiceResult> RunBackupAsync(Guid runId, BackupActor actor, BackupCancellation cancellation)
    {
        var run = await _repository.GetRunAsync(runId, CancellationToken.None);
        if (run is null || run.Status != BackupRunStatus.Running || run.Operation != BackupOperation.Backup || run.JobId is null)
            return ServiceResult.NotFound("Başlamayı bekleyen yedekleme bulunamadı.");

        var log = CreateLog(run.Id);
        var job = await _repository.GetJobIncludingDeletedAsync(run.JobId.Value, CancellationToken.None);
        try
        {
            var result = job is null
                ? ServiceResult.Failure("Yedekleme işi bulunamadı.")
                : await ExecuteBackupAsync(run, job, log, cancellation.Token);

            if (cancellation.Token.IsCancellationRequested)
                return await CompleteCancelledAsync(run, log, cancellation);

            await CompleteBackupAsync(run, job, log, result, actor);
            return result;
        }
        catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
        {
            return await CompleteCancelledAsync(run, log, cancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yedekleme beklenmeyen hata ile bitti. RunId: {RunId}", run.Id);
            var result = ServiceResult.Failure("Beklenmeyen bir hata oluştu; ayrıntılar uygulama loglarında.");
            await CompleteBackupAsync(run, job, log, result, actor);
            return result;
        }
    }

    public async Task<ServiceResult<BackupRestoreFormDto>> GetRestoreFormAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetRunAsync(runId, cancellationToken);
        if (run is null)
            return ServiceResult<BackupRestoreFormDto>.NotFound(RunNotFoundMessage);
        if (!run.IsArtifactAvailable())
            return ServiceResult<BackupRestoreFormDto>.Failure(ArtifactUnavailableMessage);

        var job = run.JobId is { } jobId ? await _repository.GetJobIncludingDeletedAsync(jobId, cancellationToken) : null;
        return ServiceResult<BackupRestoreFormDto>.Success(new BackupRestoreFormDto
        {
            Run = run.ToDetailsDto(false),
            DefaultServerId = run.ServerId,
            DefaultDirectory = "/",
            DefaultVolume = job?.VolumeName,
            DefaultContainer = job?.ContainerName,
            DefaultDatabase = job?.DatabaseName,
            DatabaseEngine = job?.DatabaseEngine,
            CanRestoreDatabase = job is { DatabaseEngine: not null, DatabaseUser: not null }
        });
    }

    public async Task<ServiceResult<Guid>> BeginRestoreAsync(BackupRestoreDto dto, BackupActor actor, CancellationToken cancellationToken = default)
    {
        TrimRestore(dto);
        var validation = await _restoreValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var source = await _repository.GetRunAsync(dto.RunId, cancellationToken);
        if (source is null)
            return ServiceResult<Guid>.NotFound(RunNotFoundMessage);
        if (!source.IsArtifactAvailable())
            return ServiceResult<Guid>.Failure(ArtifactUnavailableMessage);

        var server = await _serverRepository.GetByIdAsync(dto.TargetServerId!.Value, cancellationToken);
        if (server is null || server.IsDeleted)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.TargetServerId), "Hedef sunucu bulunamadı.");

        var job = source.JobId is { } jobId ? await _repository.GetJobIncludingDeletedAsync(jobId, cancellationToken) : null;
        var target = BuildRestoreTarget(source, job, dto);
        if (!target.IsSuccess)
            return ServiceResult<Guid>.Failure(target.Message ?? "Geri yükleme hedefi geçersiz.", ServiceErrorType.Validation);

        var run = new BackupRun
        {
            Operation = BackupOperation.Restore,
            Trigger = BackupTrigger.Manual,
            Status = BackupRunStatus.Running,
            JobId = source.JobId,
            JobName = source.JobName,
            SourceType = source.SourceType,
            ServerId = server.Id,
            ServerName = server.Name,
            StorageId = source.StorageId,
            StorageName = source.StorageName,
            FileName = source.FileName,
            SizeBytes = source.SizeBytes,
            IsEncrypted = source.IsEncrypted,
            SourceRunId = source.Id,
            RestoreTarget = DescribeTarget(target.Data!),
            UserName = actor.UserName,
            StartedAt = UtcNow
        };

        await _repository.AddRunAsync(run, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.BackupRestore, run, $"Kaynak: {source.FileName} | Hedef: {server.Name} {run.RestoreTarget}", true, actor, cancellationToken);
        _logger.LogInformation("Geri yükleme başlatıldı. RunId: {RunId}, SourceRunId: {SourceRunId}, ServerId: {ServerId}", run.Id, source.Id, server.Id);
        return ServiceResult<Guid>.Success(run.Id, "Geri yükleme başlatıldı.");
    }

    public async Task<ServiceResult> RunRestoreAsync(Guid restoreRunId, BackupRestoreDto dto, BackupActor actor, BackupCancellation cancellation)
    {
        var run = await _repository.GetRunAsync(restoreRunId, CancellationToken.None);
        if (run is null || run.Status != BackupRunStatus.Running || run.Operation != BackupOperation.Restore)
            return ServiceResult.NotFound("Başlamayı bekleyen geri yükleme bulunamadı.");

        var log = CreateLog(run.Id);
        try
        {
            var result = await ExecuteRestoreAsync(run, dto, log, cancellation.Token);
            if (cancellation.Token.IsCancellationRequested)
                return await CompleteCancelledAsync(run, log, cancellation);

            await CompleteRestoreAsync(run, log, result, actor);
            return result;
        }
        catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
        {
            return await CompleteCancelledAsync(run, log, cancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geri yükleme beklenmeyen hata ile bitti. RunId: {RunId}", run.Id);
            var result = ServiceResult.Failure("Beklenmeyen bir hata oluştu; ayrıntılar uygulama loglarında.");
            await CompleteRestoreAsync(run, log, result, actor);
            return result;
        }
    }

    public async Task<ServiceResult<BackupDownload>> OpenDownloadAsync(Guid runId, bool decrypt, BackupActor actor, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetRunAsync(runId, cancellationToken);
        if (run is null)
            return ServiceResult<BackupDownload>.NotFound(RunNotFoundMessage);
        if (!run.IsArtifactAvailable())
            return ServiceResult<BackupDownload>.Failure(ArtifactUnavailableMessage);

        var storage = await ResolveStorageAsync(run.StorageId, cancellationToken);
        if (!storage.IsSuccess)
            return ServiceResult<BackupDownload>.Failure(storage.Message ?? "Depolama hedefi kullanılamıyor.");

        var decrypting = decrypt && run.IsEncrypted;
        string? passphrase = null;
        if (decrypting)
        {
            var unprotected = UnprotectPassphrase(run);
            if (!unprotected.IsSuccess)
                return ServiceResult<BackupDownload>.Failure(unprotected.Message!);
            passphrase = unprotected.Data;
        }

        var target = storage.Data!;
        var opened = await target.Provider.OpenReadAsync(target.Settings, run.ObjectKey!, cancellationToken);
        if (!opened.IsSuccess)
            return ServiceResult<BackupDownload>.Failure(opened.Message ?? "Yedek dosyası açılamadı.");

        var fileName = run.FileName ?? Path.GetFileName(run.ObjectKey!);
        BackupDownload download;
        if (decrypting)
        {
            var raw = opened.Data!;
            var expectedHash = run.Sha256;
            var content = new ProducerReadStream(async (output, token) =>
            {
                await using var hashing = new HashingStream(raw);
                await BackupEncryption.DecryptAsync(hashing, output, passphrase!, token);
                EnsureHash(hashing, expectedHash);
            });
            download = new BackupDownload(content, BackupNames.DecryptedFileName(fileName), "application/gzip");
        }
        else
        {
            download = new BackupDownload(opened.Data!, fileName, run.IsEncrypted ? "application/octet-stream" : "application/gzip");
        }

        await AuditAsync(AuditActions.BackupDownload, run, decrypting ? "Şifresi çözülerek indirildi" : run.IsEncrypted ? "Şifreli dosya indirildi" : "İndirildi", true, actor, cancellationToken);
        return ServiceResult<BackupDownload>.Success(download);
    }

    public async Task<ServiceResult> DeleteArtifactAsync(Guid runId, BackupActor actor, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetRunAsync(runId, cancellationToken);
        if (run is null)
            return ServiceResult.NotFound(RunNotFoundMessage);
        if (!run.IsArtifactAvailable())
            return ServiceResult.Failure(ArtifactUnavailableMessage);

        var deleted = await DeleteArtifactCoreAsync(run, actor.UserName, cancellationToken);
        if (!deleted.IsSuccess)
            return deleted;

        await AuditAsync(AuditActions.BackupArtifactDelete, run, $"Dosya: {run.FileName}", true, actor, cancellationToken);
        return ServiceResult.Success("Yedek dosyası silindi. Çalışma kaydı geçmişte kalır.");
    }

    public Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default) =>
        _repository.InterruptRunningAsync(UtcNow, InterruptedReason, cancellationToken);

    private async Task<ServiceResult> ExecuteBackupAsync(BackupRun run, BackupJob job, BackupRunLog log, CancellationToken cancellationToken)
    {
        await log.InfoAsync($"{job.Name}: {BackupSourceDescriber.TypeName(job.SourceType)} yedeği ({BackupSourceDescriber.Describe(job)}) → {run.StorageName}");

        var connection = await _connectionProvider.GetAsync(job.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.");

        var storage = await ResolveStorageAsync(job.StorageId, cancellationToken);
        if (!storage.IsSuccess)
            return ServiceResult.Failure(storage.Message ?? "Depolama hedefi kullanılamıyor.");

        var spec = BuildSourceSpec(job);
        if (!spec.IsSuccess)
            return ServiceResult.Failure(spec.Message!);

        string? passphrase = null;
        if (run.IsEncrypted)
        {
            var unprotected = UnprotectPassphrase(run);
            if (!unprotected.IsSuccess)
                return ServiceResult.Failure(unprotected.Message!);
            passphrase = unprotected.Data;
        }

        await log.InfoAsync(passphrase is null
            ? "Sunucuda gzip ile sıkıştırılıyor; şifreleme kapalı."
            : "Sunucuda gzip ile sıkıştırılıyor; panelde AES-256-GCM ile şifreleniyor.");

        var target = storage.Data!;
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: PipePauseThreshold, resumeWriterThreshold: PipePauseThreshold / 2, useSynchronizationContext: false));
        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var hashing = new HashingStream(pipe.Reader.AsStream());

        var uploadTask = Task.Run(async () =>
        {
            try
            {
                var uploaded = await target.Provider.UploadAsync(target.Settings, run.ObjectKey!, hashing, transferCts.Token);
                if (!uploaded.IsSuccess)
                    await transferCts.CancelAsync();
                return uploaded;
            }
            catch
            {
                await transferCts.CancelAsync();
                throw;
            }
            finally
            {
                await hashing.DisposeAsync();
            }
        }, CancellationToken.None);

        using var progressCts = new CancellationTokenSource();
        var progressTask = ReportProgressAsync(log, () => hashing.BytesTransferred, progressCts.Token);

        ServiceResult exportResult;
        Exception? exportError = null;
        try
        {
            exportResult = await _sourceRunner.ExportAsync(connection.Data!, spec.Data!, async (stdout, token) =>
            {
                await using var writer = pipe.Writer.AsStream(leaveOpen: true);
                if (passphrase is null)
                    await stdout.CopyToAsync(writer, token);
                else
                    await BackupEncryption.EncryptAsync(stdout, writer, passphrase, _options.KeyDerivationIterations, token);
                await writer.FlushAsync(token);
            }, TimeSpan.FromMinutes(Math.Max(1, _options.BackupTimeoutMinutes)), transferCts.Token);
        }
        catch (Exception ex)
        {
            exportError = ex;
            exportResult = ServiceResult.Failure(ex is BackupFormatException ? ex.Message : "Yedek akışı okunamadı.");
        }

        if (exportResult.IsSuccess)
            await pipe.Writer.CompleteAsync();
        else
            await pipe.Writer.CompleteAsync(new IOException("Yedek akışı yarıda kesildi."));

        ServiceResult uploadResult;
        try
        {
            uploadResult = await uploadTask;
        }
        catch (Exception ex)
        {
            if (exportResult.IsSuccess)
                _logger.LogError(ex, "Yedek depolamaya yüklenemedi. RunId: {RunId}", run.Id);
            uploadResult = ServiceResult.Failure("Yedek depolamaya yüklenemedi; ayrıntılar uygulama loglarında.");
        }

        await progressCts.CancelAsync();
        await progressTask;

        cancellationToken.ThrowIfCancellationRequested();

        if (!exportResult.IsSuccess)
        {
            if (!uploadResult.IsSuccess && (exportError is OperationCanceledException || transferCts.IsCancellationRequested))
                return ServiceResult.Failure(uploadResult.Message ?? "Yedek depolamaya yüklenemedi.");

            if (exportError is not null and not OperationCanceledException and not BackupFormatException)
                _logger.LogError(exportError, "Yedek akışı okunamadı. RunId: {RunId}", run.Id);
            return exportResult;
        }

        if (!uploadResult.IsSuccess)
            return ServiceResult.Failure(uploadResult.Message ?? "Yedek depolamaya yüklenemedi.");

        run.SizeBytes = hashing.BytesTransferred;
        run.Sha256 = hashing.Sha256Hex;
        await log.InfoAsync($"Yüklendi: {run.ObjectKey} ({BackupRunLog.FormatSize(hashing.BytesTransferred)}, SHA-256 {run.Sha256})");
        return ServiceResult.Success("Yedekleme tamamlandı.");
    }

    private async Task<ServiceResult> ExecuteRestoreAsync(BackupRun run, BackupRestoreDto dto, BackupRunLog log, CancellationToken cancellationToken)
    {
        var source = run.SourceRunId is { } sourceId ? await _repository.GetRunAsync(sourceId, cancellationToken) : null;
        if (source is null || !source.IsArtifactAvailable())
            return ServiceResult.Failure(ArtifactUnavailableMessage);

        var job = source.JobId is { } jobId ? await _repository.GetJobIncludingDeletedAsync(jobId, cancellationToken) : null;
        var spec = BuildRestoreTarget(source, job, dto);
        if (!spec.IsSuccess)
            return ServiceResult.Failure(spec.Message!);

        await log.InfoAsync($"{source.FileName} → {run.ServerName} {run.RestoreTarget}");

        var connection = await _connectionProvider.GetAsync(run.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.");

        var storage = await ResolveStorageAsync(source.StorageId, cancellationToken);
        if (!storage.IsSuccess)
            return ServiceResult.Failure(storage.Message ?? "Depolama hedefi kullanılamıyor.");

        string? passphrase = null;
        if (source.IsEncrypted)
        {
            var unprotected = UnprotectPassphrase(source);
            if (!unprotected.IsSuccess)
                return ServiceResult.Failure(unprotected.Message!);
            passphrase = unprotected.Data;
        }

        var target = storage.Data!;
        var opened = await target.Provider.OpenReadAsync(target.Settings, source.ObjectKey!, cancellationToken);
        if (!opened.IsSuccess)
            return ServiceResult.Failure(opened.Message ?? "Yedek dosyası açılamadı.");

        await log.InfoAsync(passphrase is null ? "Yedek sunucuya aktarılıyor." : "Yedeğin şifresi çözülüp sunucuya aktarılıyor.");

        await using var hashing = new HashingStream(opened.Data!);
        using var progressCts = new CancellationTokenSource();
        var progressTask = ReportProgressAsync(log, () => hashing.BytesTransferred, progressCts.Token);

        ServiceResult result;
        try
        {
            result = await _sourceRunner.ImportAsync(connection.Data!, spec.Data!, async (stdin, token) =>
            {
                if (passphrase is null)
                    await hashing.CopyToAsync(stdin, token);
                else
                    await BackupEncryption.DecryptAsync(hashing, stdin, passphrase, token);
                EnsureHash(hashing, source.Sha256);
            }, TimeSpan.FromMinutes(Math.Max(1, _options.RestoreTimeoutMinutes)), cancellationToken);
        }
        catch (BackupFormatException ex)
        {
            result = ServiceResult.Failure(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Geri yükleme akışı yazılamadı. RunId: {RunId}", run.Id);
            result = ServiceResult.Failure("Yedek sunucuya aktarılamadı; ayrıntılar uygulama loglarında.");
        }
        finally
        {
            await progressCts.CancelAsync();
            await progressTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsSuccess)
            await log.InfoAsync($"Aktarılan: {BackupRunLog.FormatSize(hashing.BytesTransferred)}");
        return result.IsSuccess ? ServiceResult.Success("Geri yükleme tamamlandı.") : result;
    }

    private async Task CompleteBackupAsync(BackupRun run, BackupJob? job, BackupRunLog log, ServiceResult result, BackupActor actor)
    {
        var message = result.IsSuccess ? "Yedekleme tamamlandı." : result.Message ?? "Yedekleme başarısız oldu.";
        if (result.IsSuccess)
            await log.InfoAsync(message);
        else
            await log.ErrorAsync(message);

        run.Status = result.IsSuccess ? BackupRunStatus.Succeeded : BackupRunStatus.Failed;
        run.FailureReason = result.IsSuccess ? null : TextHelper.Truncate(message, 1000);
        run.CompletedAt = UtcNow;
        if (job is not null)
        {
            job.LastRunAt = run.CompletedAt;
            job.LastRunStatus = run.Status;
        }

        if (result.IsSuccess && job is not null && !job.IsDeleted)
            await ApplyRetentionAsync(job, log);

        run.Log = log.ToString();
        await _repository.SaveChangesAsync(CancellationToken.None);

        var size = run.SizeBytes is { } bytes ? $" | Boyut: {BackupRunLog.FormatSize(bytes)}" : string.Empty;
        await AuditAsync(AuditActions.BackupComplete, run, $"Sonuç: {message}{size}", result.IsSuccess, actor, CancellationToken.None);
    }

    private async Task CompleteRestoreAsync(BackupRun run, BackupRunLog log, ServiceResult result, BackupActor actor)
    {
        var message = result.IsSuccess ? "Geri yükleme tamamlandı." : result.Message ?? "Geri yükleme başarısız oldu.";
        if (result.IsSuccess)
            await log.InfoAsync(message);
        else
            await log.ErrorAsync(message);

        run.Status = result.IsSuccess ? BackupRunStatus.Succeeded : BackupRunStatus.Failed;
        run.FailureReason = result.IsSuccess ? null : TextHelper.Truncate(message, 1000);
        run.CompletedAt = UtcNow;
        run.Log = log.ToString();
        await _repository.SaveChangesAsync(CancellationToken.None);

        await AuditAsync(AuditActions.BackupComplete, run, $"Geri yükleme sonucu: {message}", result.IsSuccess, actor, CancellationToken.None);
    }

    private async Task<ServiceResult> CompleteCancelledAsync(BackupRun run, BackupRunLog log, BackupCancellation cancellation)
    {
        var byUser = cancellation.IsCancelledByUser && !cancellation.IsShutdown;
        var reason = byUser ? CancelledReason : InterruptedReason;
        await log.ErrorAsync(reason);

        run.Status = byUser ? BackupRunStatus.Cancelled : BackupRunStatus.Interrupted;
        run.FailureReason = reason;
        run.CancelledBy = byUser ? cancellation.CancelledBy?.UserName : null;
        run.CompletedAt = UtcNow;
        run.Log = log.ToString();
        await _repository.SaveChangesAsync(CancellationToken.None);

        var actor = byUser && cancellation.CancelledBy is not null ? cancellation.CancelledBy : BackupActor.System;
        await AuditAsync(AuditActions.BackupCancel, run, reason, false, actor, CancellationToken.None);
        return ServiceResult.Failure(reason);
    }

    /// <summary>Kayıtlar silinmez; yalnızca işin klasöründeki, ad kalıbına uyan eski yedek dosyaları silinir.</summary>
    private async Task ApplyRetentionAsync(BackupJob job, BackupRunLog log)
    {
        try
        {
            var available = await _repository.GetAvailableBackupsAsync(job.Id, CancellationToken.None);
            var expired = BackupRetention.SelectExpired(available, r => r.CompletedAt ?? r.StartedAt, job.KeepLast, job.KeepDays, UtcNow);
            if (expired.Count == 0)
                return;

            var deleted = 0;
            foreach (var old in expired)
            {
                var result = await DeleteArtifactCoreAsync(old, RetentionActor, CancellationToken.None);
                if (result.IsSuccess)
                    deleted++;
                else
                    await log.ErrorAsync($"Eski yedek silinemedi ({old.FileName}): {result.Message}");
            }

            await log.InfoAsync($"Saklama politikası: {deleted} eski yedek dosyası silindi (son {job.KeepLast} yedek{(job.KeepDays > 0 ? $" ve son {job.KeepDays} gün" : string.Empty)} korunur).");
            _logger.LogInformation("Yedek saklama politikası uygulandı. JobId: {JobId}, Deleted: {Deleted}, Expired: {Expired}", job.Id, deleted, expired.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yedek saklama politikası uygulanamadı. JobId: {JobId}", job.Id);
            await log.ErrorAsync("Saklama politikası uygulanamadı; ayrıntılar uygulama loglarında.");
        }
    }

    private async Task<ServiceResult> DeleteArtifactCoreAsync(BackupRun run, string? deletedBy, CancellationToken cancellationToken)
    {
        if (run.JobId is not { } jobId || !BackupNames.IsJobObjectKey(jobId, run.ObjectKey))
        {
            _logger.LogWarning("Yedek dosyası izin listesi dışında; silinmedi. RunId: {RunId}", run.Id);
            return ServiceResult.Failure("Dosya adı beklenen kalıpta değil; güvenlik için silinmedi.");
        }

        var storage = await ResolveStorageAsync(run.StorageId, cancellationToken);
        if (!storage.IsSuccess)
            return ServiceResult.Failure(storage.Message ?? "Depolama hedefi kullanılamıyor.");

        var target = storage.Data!;
        var deleted = await target.Provider.DeleteAsync(target.Settings, run.ObjectKey!, cancellationToken);
        if (!deleted.IsSuccess)
            return deleted;

        run.ArtifactDeletedAt = UtcNow;
        run.ArtifactDeletedBy = deletedBy;
        await _repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private async Task<ServiceResult<BackupStorageTarget>> ResolveStorageAsync(Guid? storageId, CancellationToken cancellationToken)
    {
        var storage = storageId is { } id ? await _repository.GetStorageIncludingDeletedAsync(id, cancellationToken) : null;
        return storage is null
            ? ServiceResult<BackupStorageTarget>.Failure("Depolama hedefi bulunamadı.")
            : _storageService.Resolve(storage);
    }

    private ServiceResult<BackupSourceSpec> BuildSourceSpec(BackupJob job)
    {
        var password = UnprotectDatabasePassword(job);
        if (!password.IsSuccess)
            return ServiceResult<BackupSourceSpec>.Failure(password.Message!);

        return ServiceResult<BackupSourceSpec>.Success(new BackupSourceSpec
        {
            Type = job.SourceType,
            Paths = BackupPaths.SplitLines(job.Paths),
            Excludes = BackupPaths.SplitLines(job.Excludes),
            VolumeName = job.VolumeName,
            Engine = job.DatabaseEngine,
            ContainerName = job.ContainerName,
            DatabaseName = job.DatabaseName,
            DatabaseUser = job.DatabaseUser,
            DatabasePassword = password.Data,
            DatabaseHost = job.DatabaseHost,
            DatabasePort = job.DatabasePort
        });
    }

    private ServiceResult<BackupSourceSpec> BuildRestoreTarget(BackupRun source, BackupJob? job, BackupRestoreDto dto)
    {
        switch (source.SourceType)
        {
            case BackupSourceType.Files:
                return ServiceResult<BackupSourceSpec>.Success(new BackupSourceSpec
                {
                    Type = BackupSourceType.Files,
                    TargetDirectory = BackupPaths.Normalize(dto.TargetDirectory ?? "/") ?? "/"
                });

            case BackupSourceType.DockerVolume:
                var volume = dto.TargetVolume ?? job?.VolumeName;
                return string.IsNullOrEmpty(volume)
                    ? ServiceResult<BackupSourceSpec>.Failure("Hedef volume adını girin.")
                    : ServiceResult<BackupSourceSpec>.Success(new BackupSourceSpec { Type = BackupSourceType.DockerVolume, VolumeName = volume });

            case BackupSourceType.Database:
                if (job is not { DatabaseEngine: not null, DatabaseUser: not null })
                    return ServiceResult<BackupSourceSpec>.Failure("Veritabanı bilgileri bulunamadı; yedeği indirip elle geri yükleyin.");

                var password = UnprotectDatabasePassword(job);
                if (!password.IsSuccess)
                    return ServiceResult<BackupSourceSpec>.Failure(password.Message!);

                var container = dto.TargetContainer ?? job.ContainerName;
                return ServiceResult<BackupSourceSpec>.Success(new BackupSourceSpec
                {
                    Type = BackupSourceType.Database,
                    Engine = job.DatabaseEngine,
                    ContainerName = container,
                    DatabaseName = dto.TargetDatabase ?? job.DatabaseName,
                    DatabaseUser = job.DatabaseUser,
                    DatabasePassword = password.Data,
                    DatabaseHost = container is null ? job.DatabaseHost : null,
                    DatabasePort = container is null ? job.DatabasePort : null
                });

            default:
                return ServiceResult<BackupSourceSpec>.Failure("Bilinmeyen yedek türü.");
        }
    }

    private static string DescribeTarget(BackupSourceSpec target) => target.Type switch
    {
        BackupSourceType.Files => target.TargetDirectory == "/" ? "(özgün konumlar)" : $"klasör: {target.TargetDirectory}",
        BackupSourceType.DockerVolume => $"volume: {target.VolumeName}",
        BackupSourceType.Database => $"{BackupSourceDescriber.EngineName(target.Engine)}: {target.DatabaseName}" +
                                     (string.IsNullOrEmpty(target.ContainerName) ? string.Empty : $" (container: {target.ContainerName})"),
        _ => string.Empty
    };

    private ServiceResult<string?> UnprotectDatabasePassword(BackupJob job)
    {
        if (string.IsNullOrEmpty(job.EncryptedDatabasePassword))
            return ServiceResult<string?>.Success(null);

        try
        {
            return ServiceResult<string?>.Success(_secretProtector.Unprotect(job.EncryptedDatabasePassword));
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Veritabanı parolası çözülemedi. JobId: {JobId}", job.Id);
            return ServiceResult<string?>.Failure("Veritabanı parolası çözülemedi. Master key değişmiş olabilir; işi düzenleyip parolayı yeniden girin.");
        }
    }

    private ServiceResult<string> UnprotectPassphrase(BackupRun run)
    {
        if (string.IsNullOrEmpty(run.EncryptedPassphrase))
            return ServiceResult<string>.Failure("Bu yedeğin şifreleme parolası panelde kayıtlı değil; dosyayı şifreli indirip parolayla tools/backup-decrypt.py ile açın.");

        try
        {
            return ServiceResult<string>.Success(_secretProtector.Unprotect(run.EncryptedPassphrase));
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Yedek şifreleme parolası çözülemedi. RunId: {RunId}", run.Id);
            return ServiceResult<string>.Failure("Yedek şifreleme parolası çözülemedi (master key değişmiş olabilir). Dosyayı şifreli indirip parolayla tools/backup-decrypt.py ile açın.");
        }
    }

    private static void EnsureHash(HashingStream hashing, string? expected)
    {
        if (expected is not null && !string.Equals(hashing.Sha256Hex, expected, StringComparison.OrdinalIgnoreCase))
            throw new BackupFormatException("Yedek dosyasının SHA-256 özeti kayıtlı değerle eşleşmiyor; dosya bozulmuş veya değiştirilmiş olabilir.");
    }

    private async Task ReportProgressAsync(BackupRunLog log, Func<long> bytes, CancellationToken cancellationToken)
    {
        try
        {
            var last = -1L;
            while (true)
            {
                await Task.Delay(ProgressInterval, _timeProvider, cancellationToken);
                var current = bytes();
                if (current == last)
                    continue;

                last = current;
                await log.InfoAsync($"Aktarılan: {BackupRunLog.FormatSize(current)}");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private BackupRunLog CreateLog(Guid runId) => new(
        Math.Max(64, _options.MaxStoredLogKilobytes) * 1024,
        (text, ct) => _repository.UpdateRunLogAsync(runId, text, ct),
        _timeProvider,
        BackupSchedule.ResolveTimeZone(_options.TimeZone));

    private static void TrimRestore(BackupRestoreDto dto)
    {
        dto.TargetDirectory = NullIfEmpty(dto.TargetDirectory);
        dto.TargetVolume = NullIfEmpty(dto.TargetVolume);
        dto.TargetContainer = NullIfEmpty(dto.TargetContainer);
        dto.TargetDatabase = NullIfEmpty(dto.TargetDatabase);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task AuditAsync(string action, BackupRun run, string details, bool isSuccess, BackupActor actor, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.BackupRun,
            run.Id.ToString(),
            run.JobName,
            details,
            isSuccess,
            UserNameOverride: actor.UserName,
            UserIdOverride: actor.UserId,
            IpAddressOverride: actor.IpAddress), cancellationToken);
}

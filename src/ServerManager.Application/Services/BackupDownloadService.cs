using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

/// <summary>
/// Yedek dosyasını depolamadan panel üzerinden indirir. Dosya hiçbir zaman belleğe veya diske alınmaz; depolama akışı
/// doğrudan yanıta aktarılır. Çözülmüş indirmede parola yalnızca kullanıcıdan alınır (panelde kayıtlı parola kullanılmaz).
/// </summary>
public sealed class BackupDownloadService : IBackupDownloadService
{
    public const int MaxPassphraseLength = 1024;

    private const string RunNotFoundMessage = "Yedek kaydı bulunamadı.";

    private readonly IBackupRepository _repository;
    private readonly IBackupStorageService _storageService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<BackupDownloadService> _logger;

    public BackupDownloadService(
        IBackupRepository repository,
        IBackupStorageService storageService,
        IAuditLogService auditLogService,
        ILogger<BackupDownloadService> logger)
    {
        _repository = repository;
        _storageService = storageService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<ServiceResult<BackupDownload>> OpenAsync(Guid runId, string? passphrase, BackupActor actor, CancellationToken cancellationToken = default)
    {
        var run = await _repository.GetRunAsync(runId, cancellationToken);
        if (run is null)
            return ServiceResult<BackupDownload>.NotFound(RunNotFoundMessage);
        if (BackupDownloads.UnavailableReason(run) is { } reason)
            return ServiceResult<BackupDownload>.Failure(reason, ServiceErrorType.Conflict);

        var decrypting = !string.IsNullOrEmpty(passphrase);
        if (decrypting && !run.IsEncrypted)
            return ServiceResult<BackupDownload>.Failure("Bu yedek şifreli değil; parola girmeden indirin.", ServiceErrorType.Validation);
        if (decrypting && passphrase!.Length > MaxPassphraseLength)
            return ServiceResult<BackupDownload>.Failure("Parola çok uzun.", ServiceErrorType.Validation);

        var storage = run.StorageId is { } storageId ? await _repository.GetStorageIncludingDeletedAsync(storageId, cancellationToken) : null;
        if (storage is null)
            return await FailAsync(run, "Yedeğin depolama hedefi bulunamadı.", actor, cancellationToken);

        var target = _storageService.Resolve(storage);
        if (!target.IsSuccess)
            return await FailAsync(run, $"Depolama hedefi kullanılamıyor: {target.Message}", actor, cancellationToken);

        var provider = target.Data!.Provider;
        ServiceResult<Stream> opened;
        try
        {
            opened = await provider.OpenReadAsync(target.Data.Settings, run.ObjectKey!, cancellationToken);
        }
        catch (NotSupportedException)
        {
            opened = ServiceResult<Stream>.Failure($"{provider.DisplayName} depolaması dosya okumayı desteklemiyor.");
        }

        if (!opened.IsSuccess || opened.Data is null)
            return await FailAsync(run, $"Yedek dosyası depolamadan okunamadı: {opened.Message ?? "bilinmeyen hata"}", actor, cancellationToken);

        var fileName = BackupDownloads.SafeFileName(run.FileName, run.ObjectKey, run.Id);
        var raw = opened.Data;
        BackupDownload download;
        if (decrypting)
        {
            Stream verified;
            try
            {
                verified = await BackupEncryption.VerifyPassphraseAsync(raw, passphrase!, cancellationToken);
            }
            catch (Exception ex) when (ex is BackupFormatException or IOException)
            {
                await raw.DisposeAsync();
                if (ex is IOException)
                    _logger.LogWarning(ex, "Yedek dosyası okunurken hata. RunId: {RunId}", run.Id);
                return await FailAsync(run, ex is BackupFormatException ? ex.Message : "Yedek dosyası depolamadan okunamadı.", actor, cancellationToken, ServiceErrorType.Validation);
            }
            catch
            {
                await raw.DisposeAsync();
                throw;
            }

            var expectedHash = run.Sha256;
            var content = new ProducerReadStream(async (output, token) =>
            {
                await using var hashing = new HashingStream(verified);
                await BackupEncryption.DecryptAsync(hashing, output, passphrase!, token);
                if (expectedHash is not null && !string.Equals(hashing.Sha256Hex, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new BackupFormatException("Yedek dosyasının SHA-256 özeti kayıtlı değerle eşleşmiyor; dosya bozulmuş veya değiştirilmiş olabilir.");
            });
            var plainName = BackupNames.DecryptedFileName(fileName);
            var plainLength = run.SizeBytes is { } stored ? BackupEncryption.PlaintextLength(stored) : null;
            download = new BackupDownload(content, plainName, BackupDownloads.ContentType(plainName), plainLength);
        }
        else
        {
            download = new BackupDownload(raw, fileName, BackupDownloads.ContentType(fileName), KnownLength(raw) ?? run.SizeBytes);
        }

        var mode = decrypting ? "Şifresi çözülerek indirildi" : run.IsEncrypted ? "Şifreli dosya indirildi" : "İndirildi";
        try
        {
            await AuditAsync(run, $"{mode} | Dosya: {download.FileName} | Boyut: {BackupRunLog.FormatSize(run.SizeBytes ?? 0)} | Çalışma: {run.Id} | Depolama: {run.StorageName}", true, actor, cancellationToken);
        }
        catch
        {
            await download.Content.DisposeAsync();
            throw;
        }

        _logger.LogInformation("Yedek indiriliyor. RunId: {RunId}, JobId: {JobId}, Decrypt: {Decrypt}", run.Id, run.JobId, decrypting);
        return ServiceResult<BackupDownload>.Success(download);
    }

    private static long? KnownLength(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private async Task<ServiceResult<BackupDownload>> FailAsync(
        BackupRun run, string message, BackupActor actor, CancellationToken cancellationToken, ServiceErrorType errorType = ServiceErrorType.Failure)
    {
        await AuditAsync(run, $"İndirme başarısız: {message} | Çalışma: {run.Id}", false, actor, cancellationToken);
        return ServiceResult<BackupDownload>.Failure(message, errorType);
    }

    private Task AuditAsync(BackupRun run, string details, bool isSuccess, BackupActor actor, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            AuditActions.BackupDownload,
            AuditEntityTypes.BackupRun,
            run.Id.ToString(),
            run.JobName,
            details,
            isSuccess,
            UserNameOverride: actor.UserName,
            UserIdOverride: actor.UserId,
            IpAddressOverride: actor.IpAddress), cancellationToken);
}

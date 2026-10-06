using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public sealed class ServiceAutoBackupQueue : IServiceAutoBackupQueue
{
    public const string FailedInstallMessage = "Kurulum başarıyla tamamlanmadığı için otomatik yedekleme işi oluşturulmadı.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IManagedServiceRepository _repository;
    private readonly IManagedServiceBackupService _backups;
    private readonly ISecretProtector _protector;
    private readonly ILogger<ServiceAutoBackupQueue> _logger;

    public ServiceAutoBackupQueue(
        IManagedServiceRepository repository,
        IManagedServiceBackupService backups,
        ISecretProtector protector,
        ILogger<ServiceAutoBackupQueue> logger)
    {
        _repository = repository;
        _backups = backups;
        _protector = protector;
        _logger = logger;
    }

    public async Task<ServiceResult> EnqueueAsync(Guid operationId, ServiceBackupOptionsDto options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var payload = _protector.Protect(JsonSerializer.Serialize(options, JsonOptions));
        return await _repository.SetPendingAutoBackupAsync(operationId, payload, cancellationToken)
            ? ServiceResult.Success()
            : ServiceResult.NotFound("Servis işlemi bulunamadı.");
    }

    public async Task<string?> ProcessAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _repository.GetOperationAsync(operationId, cancellationToken);
        if (operation is null || operation.Status == ManagedServiceOperationStatus.Running)
            return null;

        var payload = await _repository.ClaimPendingAutoBackupAsync(operationId, cancellationToken);
        if (payload is null)
            return null;

        if (operation.Status != ManagedServiceOperationStatus.Succeeded)
        {
            _logger.LogInformation("Kurulum başarısız; otomatik yedek isteği düşürüldü. OperationId: {OperationId}", operationId);
            return ServiceConsole.Warning(FailedInstallMessage);
        }

        ServiceBackupOptionsDto? options;
        try
        {
            options = JsonSerializer.Deserialize<ServiceBackupOptionsDto>(_protector.Unprotect(payload), JsonOptions);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            _logger.LogError(ex, "Otomatik yedek isteği çözülemedi. OperationId: {OperationId}", operationId);
            options = null;
        }

        if (options is null)
            return ServiceConsole.Warning("Otomatik yedek ayarları okunamadı; servis sayfasındaki \"Yedek işi oluştur\" ile oluşturun.");

        var result = await _backups.CreateJobAsync(operation.ServiceId, options, cancellationToken);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Kurulum sonrası otomatik yedek işi oluşturuldu. OperationId: {OperationId}, JobId: {JobId}", operationId, result.Data);
            return ServiceConsole.Success("Otomatik yedekleme işi oluşturuldu (Yedekleme → İşler).");
        }

        var reason = result.Errors.FirstOrDefault()?.Message ?? result.Message ?? "bilinmeyen hata";
        _logger.LogWarning("Kurulum sonrası otomatik yedek işi oluşturulamadı. OperationId: {OperationId}, Reason: {Reason}", operationId, reason);
        return ServiceConsole.Warning($"Otomatik yedekleme işi oluşturulamadı: {reason} Servis sayfasındaki \"Yedek işi oluştur\" ile yeniden deneyin.");
    }

    public async Task<IReadOnlyList<PendingAutoBackupOperation>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var operations = await _repository.ListPendingAutoBackupOperationsAsync(cancellationToken);
        return operations
            .Select(o => new PendingAutoBackupOperation(o.Id, o.ServiceId, o.UserId, o.UserName, o.IpAddress))
            .ToList();
    }
}

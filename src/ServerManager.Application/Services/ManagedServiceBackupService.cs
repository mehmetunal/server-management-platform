using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ManagedServiceBackupService : IManagedServiceBackupService
{
    private const int MaxJobNameLength = 128;

    private readonly IManagedServiceService _services;
    private readonly IManagedServiceRepository _serviceRepository;
    private readonly IBackupJobService _jobs;
    private readonly IBackupRepository _backups;

    public ManagedServiceBackupService(
        IManagedServiceService services,
        IManagedServiceRepository serviceRepository,
        IBackupJobService jobs,
        IBackupRepository backups)
    {
        _services = services;
        _serviceRepository = serviceRepository;
        _jobs = jobs;
        _backups = backups;
    }

    public async Task<IReadOnlyList<BackupJobListItemDto>> ListJobsAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetAsync(serviceId, cancellationToken);
        if (service is null)
            return [];

        var jobs = await _jobs.GetJobsAsync(service.ServerId, cancellationToken);
        return jobs
            .Where(j => j.SourceType == BackupSourceType.Database && string.Equals(j.ContainerName, service.ContainerName, StringComparison.Ordinal))
            .ToList();
    }

    public async Task<ServiceResult> ValidateOptionsAsync(
        string? templateKey, string? serviceDatabase, ServiceBackupOptionsDto options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var engine = ManagedServiceBackups.EngineFor(templateKey);
        if (engine is null)
            return ServiceResult.ValidationFailure("AutoBackup.Enabled", "Bu servis türü için otomatik yedek desteklenmiyor.");

        if (options.StorageId is not { } storageId || storageId == Guid.Empty || await _backups.GetStorageAsync(storageId, cancellationToken) is null)
            return ServiceResult.ValidationFailure("AutoBackup.StorageId", "Yedeklerin yazılacağı depolama hedefini seçin.");

        if (options.ScheduleType is not (BackupScheduleType.Daily or BackupScheduleType.Hourly))
            return ServiceResult.ValidationFailure("AutoBackup.ScheduleType", "Zamanlama günlük veya saat aralıklı olmalıdır.");

        if (options.ScheduleType == BackupScheduleType.Hourly && (options.ScheduleIntervalHours < BackupSchedule.MinIntervalHours || options.ScheduleIntervalHours > BackupSchedule.MaxIntervalHours))
        {
            return ServiceResult.ValidationFailure("AutoBackup.ScheduleIntervalHours",
                $"Aralık {BackupSchedule.MinIntervalHours} ile {BackupSchedule.MaxIntervalHours} saat arasında olmalıdır.");
        }

        if (options.KeepLast < BackupRetention.MinKeepLast || options.KeepLast > BackupRetention.MaxKeepLast)
        {
            return ServiceResult.ValidationFailure("AutoBackup.KeepLast",
                $"Saklanacak yedek sayısı {BackupRetention.MinKeepLast} ile {BackupRetention.MaxKeepLast} arasında olmalıdır.");
        }

        if (!string.IsNullOrEmpty(options.EncryptionPassphrase) && options.EncryptionPassphrase.Length < 12)
            return ServiceResult.ValidationFailure("AutoBackup.EncryptionPassphrase", "Şifreleme parolası en az 12 karakter olmalıdır.");

        var database = ManagedServiceBackups.BackupDatabase(templateKey!, serviceDatabase, options.DatabaseName);
        if (BackupDatabaseEngines.RequiresDatabaseName(engine.Value) && string.IsNullOrWhiteSpace(database))
            return ServiceResult.ValidationFailure("AutoBackup.DatabaseName", "Yedeklenecek veritabanının adını girin.");

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<Guid>> CreateJobAsync(Guid serviceId, ServiceBackupOptionsDto options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var info = await _services.GetConnectionInfoAsync(serviceId, cancellationToken);
        if (!info.IsSuccess)
            return ServiceResult<Guid>.Failure(info.Message ?? "Servis bulunamadı.", info.ErrorType);

        var connection = info.Data!;
        var validation = await ValidateOptionsAsync(connection.TemplateKey, connection.Database, options, cancellationToken);
        if (!validation.IsSuccess)
            return ServiceResult<Guid>.ValidationFailure(validation.Errors);

        var request = new ContainerDatabaseBackupRequest(
            await UniqueNameAsync($"{connection.Name} ({connection.Host})", cancellationToken),
            connection.ServerId,
            options.StorageId!.Value,
            ManagedServiceBackups.EngineFor(connection.TemplateKey)!.Value,
            connection.Host,
            ManagedServiceBackups.BackupDatabase(connection.TemplateKey, connection.Database, options.DatabaseName),
            ManagedServiceBackups.BackupUser(connection.TemplateKey, connection.Username),
            connection.Password)
        {
            ScheduleType = options.ScheduleType,
            ScheduleTime = string.IsNullOrWhiteSpace(options.ScheduleTime) ? "03:00" : options.ScheduleTime.Trim(),
            ScheduleIntervalHours = options.ScheduleType == BackupScheduleType.Hourly ? options.ScheduleIntervalHours : 24,
            KeepLast = options.KeepLast,
            EncryptionPassphrase = string.IsNullOrEmpty(options.EncryptionPassphrase) ? null : options.EncryptionPassphrase
        };

        return await _jobs.CreateForContainerDatabaseAsync(request, cancellationToken);
    }

    private async Task<string> UniqueNameAsync(string baseName, CancellationToken cancellationToken)
    {
        var name = TextHelper.Truncate(baseName, MaxJobNameLength - 5)!;
        if (!await _backups.JobNameExistsAsync(name, null, cancellationToken))
            return name;

        for (var i = 2; i < 100; i++)
        {
            var candidate = $"{name} #{i}";
            if (!await _backups.JobNameExistsAsync(candidate, null, cancellationToken))
                return candidate;
        }

        return TextHelper.Truncate($"{name} {Guid.NewGuid():N}", MaxJobNameLength)!;
    }
}

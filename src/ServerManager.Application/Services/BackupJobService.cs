using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Validators.Backups;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class BackupJobService : IBackupJobService
{
    private const string NotFoundMessage = "Yedekleme işi bulunamadı.";

    private readonly IBackupRepository _repository;
    private readonly IServerRepository _serverRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<BackupJobFormDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupJobService> _logger;

    public BackupJobService(
        IBackupRepository repository,
        IServerRepository serverRepository,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<BackupJobFormDto> validator,
        TimeProvider timeProvider,
        IOptions<BackupOptions> options,
        ILogger<BackupJobService> logger)
    {
        _repository = repository;
        _serverRepository = serverRepository;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<BackupJobListItemDto>> GetJobsAsync(Guid? serverId = null, CancellationToken cancellationToken = default)
    {
        var jobs = await _repository.GetJobsAsync(serverId, cancellationToken);
        var latest = await _repository.GetLatestAvailableBackupsAsync(jobs.Select(j => j.Id).ToList(), cancellationToken);
        return jobs.Select(job => ToListItem(job, latest.GetValueOrDefault(job.Id))).ToList();
    }

    public async Task<ServiceResult<BackupJobDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobIncludingDeletedAsync(id, cancellationToken);
        if (job is null)
            return ServiceResult<BackupJobDetailsDto>.NotFound(NotFoundMessage);

        var latest = await _repository.GetLatestAvailableBackupsAsync([job.Id], cancellationToken);
        return ServiceResult<BackupJobDetailsDto>.Success(new BackupJobDetailsDto
        {
            Job = ToListItem(job, latest.GetValueOrDefault(job.Id)),
            Paths = BackupPaths.SplitLines(job.Paths),
            Excludes = BackupPaths.SplitLines(job.Excludes),
            VolumeName = job.VolumeName,
            DatabaseEngine = job.DatabaseEngine,
            ContainerName = job.ContainerName,
            DatabaseName = job.DatabaseName,
            DatabaseUser = job.DatabaseUser,
            DatabaseAuthSource = job.DatabaseAuthSource,
            HasDatabasePassword = !string.IsNullOrEmpty(job.EncryptedDatabasePassword),
            DatabaseHost = job.DatabaseHost,
            DatabasePort = job.DatabasePort,
            CreatedAt = job.CreatedAt,
            CreatedBy = job.CreatedBy,
            UpdatedAt = job.UpdatedAt,
            UpdatedBy = job.UpdatedBy
        });
    }

    public async Task<ServiceResult<BackupJobFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(id, cancellationToken);
        if (job is null)
            return ServiceResult<BackupJobFormDto>.NotFound(NotFoundMessage);

        return ServiceResult<BackupJobFormDto>.Success(new BackupJobFormDto
        {
            Id = job.Id,
            Name = job.Name,
            ServerId = job.ServerId,
            StorageId = job.StorageId,
            SourceType = job.SourceType,
            Paths = job.Paths,
            Excludes = job.Excludes,
            VolumeName = job.VolumeName,
            DatabaseEngine = job.DatabaseEngine ?? BackupDatabaseEngine.PostgreSql,
            ContainerName = job.ContainerName,
            DatabaseName = job.DatabaseName,
            DatabaseUser = job.DatabaseUser,
            DatabaseAuthSource = job.DatabaseAuthSource,
            HasStoredDatabasePassword = !string.IsNullOrEmpty(job.EncryptedDatabasePassword),
            DatabaseHost = job.DatabaseHost,
            DatabasePort = job.DatabasePort,
            EncryptionEnabled = job.EncryptionEnabled,
            HasStoredPassphrase = !string.IsNullOrEmpty(job.EncryptedPassphrase),
            ScheduleType = job.ScheduleType,
            ScheduleIntervalHours = job.ScheduleIntervalHours,
            ScheduleTime = $"{job.ScheduleMinuteOfDay / 60:00}:{job.ScheduleMinuteOfDay % 60:00}",
            ScheduleDayOfWeek = job.ScheduleDayOfWeek ?? DayOfWeek.Sunday,
            KeepLast = job.KeepLast,
            KeepDays = job.KeepDays,
            IsEnabled = job.IsEnabled
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(BackupJobFormDto dto, CancellationToken cancellationToken = default)
    {
        Trim(dto);
        dto.HasStoredPassphrase = false;
        dto.HasStoredDatabasePassword = false;
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var references = await CheckReferencesAsync(dto, cancellationToken);
        if (references is not null)
            return ServiceResult<Guid>.ValidationFailure(references.Errors);

        if (await _repository.JobNameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir yedekleme işi zaten kayıtlı.");

        var job = new BackupJob { CreatedAt = UtcNow, CreatedBy = _currentUser.UserName };
        Apply(job, dto);

        await _repository.AddJobAsync(job, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Yedekleme işi eklendi. JobId: {JobId}, Source: {SourceType}", job.Id, job.SourceType);
        await AuditAsync(AuditActions.BackupJobCreate, job, $"{BackupSourceDescriber.TypeName(job.SourceType)} | {BackupSourceDescriber.Describe(job)} | Şifreleme: {(job.EncryptionEnabled ? "açık" : "kapalı")}", cancellationToken);
        return ServiceResult<Guid>.Success(job.Id, "Yedekleme işi eklendi.");
    }

    public Task<ServiceResult<Guid>> CreateForContainerDatabaseAsync(ContainerDatabaseBackupRequest request, CancellationToken cancellationToken = default) =>
        CreateAsync(BackupJobSpecs.ForContainerDatabase(request), cancellationToken);

    public async Task<ServiceResult> UpdateAsync(BackupJobFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var job = await _repository.GetJobAsync(id, cancellationToken);
        if (job is null)
            return ServiceResult.NotFound(NotFoundMessage);

        Trim(dto);
        dto.HasStoredPassphrase = !string.IsNullOrEmpty(job.EncryptedPassphrase);
        dto.HasStoredDatabasePassword = !string.IsNullOrEmpty(job.EncryptedDatabasePassword);
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var references = await CheckReferencesAsync(dto, cancellationToken);
        if (references is not null)
            return references;

        if (await _repository.JobNameExistsAsync(dto.Name, job.Id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir yedekleme işi zaten kayıtlı.");

        var changes = Describe(job, dto);
        Apply(job, dto);
        job.UpdatedAt = UtcNow;
        job.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.BackupJobUpdate, job, changes.Count == 0 ? "Değişiklik yok" : string.Join(" | ", changes), cancellationToken);
        return ServiceResult.Success("Yedekleme işi güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetJobAsync(id, cancellationToken);
        if (job is null)
            return ServiceResult.NotFound(NotFoundMessage);

        job.IsDeleted = true;
        job.DeletedAt = UtcNow;
        job.DeletedBy = _currentUser.UserName;
        job.NextRunAt = null;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.BackupJobDelete, job, null, cancellationToken);
        return ServiceResult.Success("Yedekleme işi silindi. Alınmış yedekler ve geçmiş korunur; geçmiş sayfasından geri yüklenebilir.");
    }

    public async Task<IReadOnlyList<ServerOptionDto>> GetServerOptionsAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.FindAsync(_ => true, cancellationToken);
        return servers
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ServerOptionDto(s.Id, s.Name, s.IpAddress))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> ClaimDueJobsAsync(int take, CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var jobs = await _repository.GetDueJobsAsync(now, take, cancellationToken);
        if (jobs.Count == 0)
            return [];

        var timeZone = BackupSchedule.ResolveTimeZone(_options.TimeZone);
        foreach (var job in jobs)
            job.NextRunAt = BackupSchedule.NextRun(job.ScheduleType, job.ScheduleIntervalHours, job.ScheduleMinuteOfDay, job.ScheduleDayOfWeek, now, timeZone);

        await _repository.SaveChangesAsync(cancellationToken);
        return jobs.Select(j => j.Id).ToList();
    }

    private async Task<ServiceResult?> CheckReferencesAsync(BackupJobFormDto dto, CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdAsync(dto.ServerId!.Value, cancellationToken);
        if (server is null || server.IsDeleted)
            return ServiceResult.ValidationFailure(nameof(dto.ServerId), "Sunucu bulunamadı.");

        var storage = await _repository.GetStorageAsync(dto.StorageId!.Value, cancellationToken);
        if (storage is null)
            return ServiceResult.ValidationFailure(nameof(dto.StorageId), "Depolama hedefi bulunamadı.");

        return null;
    }

    private void Apply(BackupJob job, BackupJobFormDto dto)
    {
        job.Name = dto.Name;
        job.ServerId = dto.ServerId!.Value;
        job.StorageId = dto.StorageId!.Value;
        job.SourceType = dto.SourceType;
        job.IsEnabled = dto.IsEnabled;

        job.Paths = dto.SourceType == BackupSourceType.Files
            ? string.Join('\n', BackupPaths.SplitLines(dto.Paths).Select(p => BackupPaths.Normalize(p)!).Distinct(StringComparer.Ordinal))
            : null;
        job.Excludes = dto.SourceType == BackupSourceType.Files && BackupPaths.SplitLines(dto.Excludes) is { Count: > 0 } excludes
            ? string.Join('\n', excludes)
            : null;
        job.VolumeName = dto.SourceType == BackupSourceType.DockerVolume ? dto.VolumeName : null;

        if (dto.SourceType == BackupSourceType.Database)
        {
            job.DatabaseEngine = dto.DatabaseEngine;
            job.ContainerName = NullIfEmpty(dto.ContainerName);
            job.DatabaseName = BackupDatabaseEngines.UsesDatabaseName(dto.DatabaseEngine) ? NullIfEmpty(dto.DatabaseName) : null;
            job.DatabaseUser = NullIfEmpty(dto.DatabaseUser);
            job.DatabaseAuthSource = BackupDatabaseEngines.UsesAuthSource(dto.DatabaseEngine) && job.DatabaseUser is not null
                ? NullIfEmpty(dto.DatabaseAuthSource) ?? BackupDatabaseEngines.DefaultMongoAuthSource
                : null;
            job.DatabaseHost = string.IsNullOrWhiteSpace(dto.ContainerName) ? NullIfEmpty(dto.DatabaseHost) : null;
            job.DatabasePort = string.IsNullOrWhiteSpace(dto.ContainerName) ? dto.DatabasePort : null;
            if (dto.ClearDatabasePassword)
                job.EncryptedDatabasePassword = null;
            else if (!string.IsNullOrEmpty(dto.DatabasePassword))
                job.EncryptedDatabasePassword = _secretProtector.Protect(dto.DatabasePassword);
        }
        else
        {
            job.DatabaseEngine = null;
            job.ContainerName = null;
            job.DatabaseName = null;
            job.DatabaseUser = null;
            job.DatabaseAuthSource = null;
            job.DatabaseHost = null;
            job.DatabasePort = null;
            job.EncryptedDatabasePassword = null;
        }

        job.EncryptionEnabled = dto.EncryptionEnabled;
        if (!string.IsNullOrEmpty(dto.Passphrase))
            job.EncryptedPassphrase = _secretProtector.Protect(dto.Passphrase);

        job.ScheduleType = dto.ScheduleType;
        job.ScheduleIntervalHours = dto.ScheduleIntervalHours;
        job.ScheduleMinuteOfDay = BackupJobFormDtoValidator.TryParseTime(dto.ScheduleTime, out var minute) ? minute : job.ScheduleMinuteOfDay;
        job.ScheduleDayOfWeek = dto.ScheduleType == BackupScheduleType.Weekly ? dto.ScheduleDayOfWeek : null;
        job.KeepLast = dto.KeepLast;
        job.KeepDays = dto.KeepDays;

        job.NextRunAt = job.IsEnabled
            ? BackupSchedule.NextRun(job.ScheduleType, job.ScheduleIntervalHours, job.ScheduleMinuteOfDay, job.ScheduleDayOfWeek, UtcNow, BackupSchedule.ResolveTimeZone(_options.TimeZone))
            : null;
    }

    private static List<string> Describe(BackupJob job, BackupJobFormDto dto)
    {
        var changes = new List<string>();
        if (job.Name != dto.Name) changes.Add($"Ad: {job.Name} -> {dto.Name}");
        if (job.ServerId != dto.ServerId) changes.Add("Sunucu değişti");
        if (job.StorageId != dto.StorageId) changes.Add("Depolama değişti");
        if (job.SourceType != dto.SourceType) changes.Add($"Tür: {BackupSourceDescriber.TypeName(job.SourceType)} -> {BackupSourceDescriber.TypeName(dto.SourceType)}");
        if (job.EncryptionEnabled != dto.EncryptionEnabled) changes.Add(dto.EncryptionEnabled ? "Şifreleme açıldı" : "Şifreleme kapatıldı");
        if (!string.IsNullOrEmpty(dto.Passphrase) && !string.IsNullOrEmpty(job.EncryptedPassphrase)) changes.Add("Şifreleme parolası değişti");
        if (!string.IsNullOrEmpty(dto.DatabasePassword) || dto.ClearDatabasePassword) changes.Add("Veritabanı parolası değişti");
        if (job.ScheduleType != dto.ScheduleType) changes.Add($"Zamanlama: {job.ScheduleType} -> {dto.ScheduleType}");
        if (job.KeepLast != dto.KeepLast || job.KeepDays != dto.KeepDays) changes.Add($"Saklama: {dto.KeepLast} yedek / {dto.KeepDays} gün");
        if (job.IsEnabled != dto.IsEnabled) changes.Add(dto.IsEnabled ? "Etkinleştirildi" : "Devre dışı bırakıldı");
        return changes;
    }

    private static BackupJobListItemDto ToListItem(BackupJob job, BackupArtifactRef? latestBackup) => new()
    {
        LatestBackup = latestBackup,
        Id = job.Id,
        Name = job.Name,
        ServerId = job.ServerId,
        ServerName = job.Server?.Name ?? "-",
        StorageId = job.StorageId,
        StorageName = job.Storage?.Name ?? "-",
        SourceType = job.SourceType,
        SourceSummary = BackupSourceDescriber.Describe(job),
        DatabaseEngine = job.DatabaseEngine,
        ContainerName = job.ContainerName,
        ScheduleType = job.ScheduleType,
        ScheduleIntervalHours = job.ScheduleIntervalHours,
        ScheduleMinuteOfDay = job.ScheduleMinuteOfDay,
        ScheduleDayOfWeek = job.ScheduleDayOfWeek,
        NextRunAt = job.NextRunAt,
        LastRunAt = job.LastRunAt,
        LastRunStatus = job.LastRunStatus,
        EncryptionEnabled = job.EncryptionEnabled,
        KeepLast = job.KeepLast,
        KeepDays = job.KeepDays,
        IsEnabled = job.IsEnabled,
        IsDeleted = job.IsDeleted
    };

    private static void Trim(BackupJobFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.Paths = dto.Paths?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        dto.Excludes = dto.Excludes?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        dto.VolumeName = dto.VolumeName?.Trim();
        dto.ContainerName = dto.ContainerName?.Trim();
        dto.DatabaseName = dto.DatabaseName?.Trim();
        dto.DatabaseUser = dto.DatabaseUser?.Trim();
        dto.DatabaseAuthSource = dto.DatabaseAuthSource?.Trim();
        dto.DatabaseHost = dto.DatabaseHost?.Trim();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private Task AuditAsync(string action, BackupJob job, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.BackupJob, job.Id.ToString(), job.Name, details), cancellationToken);
}

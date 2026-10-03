using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class UptimeService : IUptimeService
{
    public const int RecentResultCount = 50;
    public const int ProtectedResultsPerCheck = 100;
    public const int DueBatchSize = 200;

    private const string NotFoundMessage = "Uptime kontrolü bulunamadı.";

    private readonly IUptimeRepository _repository;
    private readonly IServerRepository _serverRepository;
    private readonly IUptimeProbe _probe;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<UptimeCheckFormDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly AlertingOptions _options;
    private readonly ILogger<UptimeService> _logger;

    public UptimeService(
        IUptimeRepository repository,
        IServerRepository serverRepository,
        IUptimeProbe probe,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<UptimeCheckFormDto> validator,
        TimeProvider timeProvider,
        IOptions<AlertingOptions> options,
        ILogger<UptimeService> logger)
    {
        _repository = repository;
        _serverRepository = serverRepository;
        _probe = probe;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<UptimeCheckListItemDto>> SearchAsync(UptimeFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _repository.SearchAsync(filter, cancellationToken);
        var ids = page.Items.Select(c => c.Id).ToList();
        var percents = ids.Count == 0
            ? new Dictionary<Guid, double>()
            : await _repository.GetUptimePercentAsync(ids, UtcNow.AddHours(-24), cancellationToken);
        return page.Map(c => ToListItem(c, percents.TryGetValue(c.Id, out var p) ? p : null));
    }

    public async Task<ServiceResult<UptimeCheckDetailsDto>> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var check = await _repository.GetAsync(id, cancellationToken);
        if (check is null)
            return ServiceResult<UptimeCheckDetailsDto>.NotFound(NotFoundMessage);

        var now = UtcNow;
        var ids = new[] { id };
        var day = await _repository.GetUptimePercentAsync(ids, now.AddHours(-24), cancellationToken);
        var week = await _repository.GetUptimePercentAsync(ids, now.AddDays(-7), cancellationToken);
        var month = await _repository.GetUptimePercentAsync(ids, now.AddDays(-30), cancellationToken);
        var average = await _repository.GetAverageResponseMsAsync(id, now.AddHours(-24), cancellationToken);
        var results = await _repository.GetRecentResultsAsync(id, RecentResultCount, cancellationToken);

        return ServiceResult<UptimeCheckDetailsDto>.Success(new UptimeCheckDetailsDto(
            ToListItem(check, day.TryGetValue(id, out var d) ? d : null),
            check.TimeoutSeconds,
            check.AcceptedStatusCodes,
            week.TryGetValue(id, out var w) ? w : null,
            month.TryGetValue(id, out var m) ? m : null,
            average,
            results.Select(r => new UptimeCheckResultDto(r.CheckedAt, r.IsUp, r.ResponseMs, r.StatusCode, r.Message)).ToList()));
    }

    public async Task<ServiceResult<UptimeCheckFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var check = await _repository.GetAsync(id, cancellationToken);
        if (check is null)
            return ServiceResult<UptimeCheckFormDto>.NotFound(NotFoundMessage);

        return ServiceResult<UptimeCheckFormDto>.Success(new UptimeCheckFormDto
        {
            Id = check.Id,
            Name = check.Name,
            Type = check.Type,
            Url = check.Url,
            Host = check.Host,
            Port = check.Port,
            AcceptedStatusCodes = check.AcceptedStatusCodes ?? StatusCodeRanges.Default,
            ServerId = check.ServerId,
            IntervalSeconds = check.IntervalSeconds,
            TimeoutSeconds = check.TimeoutSeconds,
            IsEnabled = check.IsEnabled
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(UptimeCheckFormDto dto, CancellationToken cancellationToken = default)
    {
        Normalize(dto);
        var error = await ValidateAsync(dto, null, cancellationToken);
        if (error is not null)
            return ServiceResult<Guid>.ValidationFailure(error.Errors);

        var check = new UptimeCheck { CreatedAt = UtcNow, CreatedBy = _currentUser.UserName, Status = UptimeStatus.Unknown };
        Apply(check, dto);
        await _repository.AddAsync(check, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.UptimeCheckCreate, check, $"{Target(check)} | {check.IntervalSeconds} sn", cancellationToken);
        return ServiceResult<Guid>.Success(check.Id, "Uptime kontrolü eklendi; ilk sonuç birkaç saniye içinde gelir.");
    }

    public async Task<ServiceResult> UpdateAsync(UptimeCheckFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var check = await _repository.GetAsync(id, cancellationToken);
        if (check is null)
            return ServiceResult.NotFound(NotFoundMessage);

        Normalize(dto);
        var error = await ValidateAsync(dto, check.Id, cancellationToken);
        if (error is not null)
            return error;

        var before = Target(check);
        var targetChanged = check.Type != dto.Type
            || !string.Equals(check.Url, dto.Url, StringComparison.Ordinal)
            || !string.Equals(check.Host, dto.Host, StringComparison.OrdinalIgnoreCase)
            || check.Port != dto.Port;

        Apply(check, dto);
        check.UpdatedAt = UtcNow;
        check.UpdatedBy = _currentUser.UserName;
        if (targetChanged)
        {
            check.Status = UptimeStatus.Unknown;
            check.StatusChangedAt = null;
            check.ConsecutiveFailures = 0;
            check.LastCheckedAt = null;
            check.LastError = null;
            check.LastStatusCode = null;
            check.LastResponseMs = null;
        }

        await _repository.SaveChangesAsync(cancellationToken);

        var after = Target(check);
        await AuditAsync(AuditActions.UptimeCheckUpdate, check, before == after ? after : $"{before} -> {after}", cancellationToken);
        return ServiceResult.Success("Uptime kontrolü güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var check = await _repository.GetAsync(id, cancellationToken);
        if (check is null)
            return ServiceResult.NotFound(NotFoundMessage);

        check.IsDeleted = true;
        check.DeletedAt = UtcNow;
        check.DeletedBy = _currentUser.UserName;
        check.IsEnabled = false;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.UptimeCheckDelete, check, Target(check), cancellationToken);
        return ServiceResult.Success("Uptime kontrolü silindi.");
    }

    public async Task<ServiceResult<UptimeProbeResult>> CheckNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var check = await _repository.GetAsync(id, cancellationToken);
        if (check is null)
            return ServiceResult<UptimeProbeResult>.NotFound(NotFoundMessage);

        var result = await ProbeAndRecordAsync(check, cancellationToken);
        return ServiceResult<UptimeProbeResult>.Success(result, result.IsUp
            ? $"Erişilebilir ({result.ResponseMs} ms)."
            : $"Erişilemiyor: {result.Message}");
    }

    public Task<IReadOnlyList<Guid>> GetDueCheckIdsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetDueIdsAsync(UtcNow, DueBatchSize, cancellationToken);

    public async Task RunCheckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var check = await _repository.GetAsync(id, cancellationToken);
        if (check is null || !check.IsEnabled)
            return;

        await ProbeAndRecordAsync(check, cancellationToken);
    }

    public async Task<int> RunMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = UtcNow.AddDays(-Math.Max(1, _options.UptimeResultRetentionDays));
        var deleted = await _repository.DeleteExpiredResultsAsync(cutoff, ProtectedResultsPerCheck, cancellationToken);
        if (deleted > 0)
            _logger.LogInformation("Uptime sonuçları temizlendi. Deleted: {Deleted}", deleted);
        return deleted;
    }

    private async Task<UptimeProbeResult> ProbeAndRecordAsync(UptimeCheck check, CancellationToken cancellationToken)
    {
        var request = new UptimeProbeRequest(check.Type, check.Url, check.Host, check.Port, check.TimeoutSeconds, check.AcceptedStatusCodes);
        var result = await _probe.ProbeAsync(request, cancellationToken);
        var now = UtcNow;
        var message = TextHelper.Truncate(result.Message, 500);

        await _repository.AddResultAsync(new UptimeCheckResult
        {
            CheckId = check.Id,
            CheckedAt = now,
            IsUp = result.IsUp,
            ResponseMs = result.ResponseMs,
            StatusCode = result.StatusCode,
            Message = message
        }, cancellationToken);

        var status = result.IsUp ? UptimeStatus.Up : UptimeStatus.Down;
        if (check.Status != status)
        {
            check.Status = status;
            check.StatusChangedAt = now;
        }

        check.LastCheckedAt = now;
        check.LastResponseMs = result.ResponseMs;
        check.LastStatusCode = result.StatusCode;
        check.LastError = result.IsUp ? null : message;
        check.ConsecutiveFailures = result.IsUp ? 0 : check.ConsecutiveFailures + 1;

        await _repository.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<ServiceResult?> ValidateAsync(UptimeCheckFormDto dto, Guid? excludeId, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var minimum = Math.Max(10, _options.UptimeMinimumIntervalSeconds);
        if (dto.IntervalSeconds < minimum)
            return ServiceResult.ValidationFailure(nameof(dto.IntervalSeconds), $"Kontrol aralığı en az {minimum} saniye olmalıdır.");

        if (dto.ServerId is { } serverId && !await _serverRepository.AnyAsync(s => s.Id == serverId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.ServerId), "Seçilen sunucu bulunamadı.");

        if (await _repository.NameExistsAsync(dto.Name, excludeId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir uptime kontrolü zaten kayıtlı.");

        return null;
    }

    private static void Normalize(UptimeCheckFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        if (dto.Type == UptimeCheckType.Http)
        {
            dto.Url = TextHelper.NullIfEmpty(dto.Url?.Trim());
            dto.AcceptedStatusCodes = TextHelper.NullIfEmpty(dto.AcceptedStatusCodes?.Replace(" ", string.Empty)) ?? StatusCodeRanges.Default;
            dto.Host = null;
            dto.Port = null;
        }
        else
        {
            dto.Host = TextHelper.NullIfEmpty(dto.Host?.Trim().ToLowerInvariant());
            dto.Url = null;
            dto.AcceptedStatusCodes = null;
        }
    }

    private static void Apply(UptimeCheck check, UptimeCheckFormDto dto)
    {
        check.Name = dto.Name;
        check.Type = dto.Type;
        check.Url = dto.Url;
        check.Host = dto.Host;
        check.Port = dto.Port;
        check.AcceptedStatusCodes = dto.AcceptedStatusCodes;
        check.ServerId = dto.ServerId;
        check.IntervalSeconds = dto.IntervalSeconds;
        check.TimeoutSeconds = dto.TimeoutSeconds;
        check.IsEnabled = dto.IsEnabled;
    }

    public static string Target(UptimeCheck check) =>
        check.Type == UptimeCheckType.Http ? check.Url ?? string.Empty : $"{check.Host}:{check.Port}";

    private static UptimeCheckListItemDto ToListItem(UptimeCheck c, double? uptime24h) => new(
        c.Id, c.Name, c.Type, Target(c), c.ServerId, c.Server?.Name, c.IntervalSeconds, c.IsEnabled, c.Status,
        c.StatusChangedAt, c.LastCheckedAt, c.LastResponseMs, c.LastStatusCode, c.LastError, c.ConsecutiveFailures, uptime24h);

    private Task AuditAsync(string action, UptimeCheck check, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.UptimeCheck, check.Id.ToString(), check.Name, details), cancellationToken);
}

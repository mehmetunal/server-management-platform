using FluentValidation;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class SslCertificateService : ISslCertificateService
{
    public const int ProbeTimeoutSeconds = 15;
    public const int DueBatchSize = 50;

    private const string NotFoundMessage = "SSL izleme kaydı bulunamadı.";

    private readonly ISslCertificateRepository _repository;
    private readonly IServerRepository _serverRepository;
    private readonly ISslCertificateProbe _probe;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<SslMonitorFormDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly AlertingOptions _options;

    public SslCertificateService(
        ISslCertificateRepository repository,
        IServerRepository serverRepository,
        ISslCertificateProbe probe,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<SslMonitorFormDto> validator,
        TimeProvider timeProvider,
        IOptions<AlertingOptions> options)
    {
        _repository = repository;
        _serverRepository = serverRepository;
        _probe = probe;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<SslMonitorListItemDto>> SearchAsync(SslMonitorFilterDto filter, CancellationToken cancellationToken = default)
    {
        var page = await _repository.SearchAsync(filter, cancellationToken);
        var now = UtcNow;
        return page.Map(m => ToListItem(m, now));
    }

    public async Task<ServiceResult<SslMonitorFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var monitor = await _repository.GetAsync(id, cancellationToken);
        if (monitor is null)
            return ServiceResult<SslMonitorFormDto>.NotFound(NotFoundMessage);

        return ServiceResult<SslMonitorFormDto>.Success(new SslMonitorFormDto
        {
            Id = monitor.Id,
            Host = monitor.Host,
            Port = monitor.Port,
            ServerId = monitor.ServerId,
            IsEnabled = monitor.IsEnabled
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(SslMonitorFormDto dto, CancellationToken cancellationToken = default)
    {
        Normalize(dto);
        var error = await ValidateAsync(dto, null, cancellationToken);
        if (error is not null)
            return ServiceResult<Guid>.ValidationFailure(error.Errors);

        var monitor = new SslCertificateMonitor
        {
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName,
            Status = SslCertificateStatus.Unknown
        };
        Apply(monitor, dto);
        await _repository.AddAsync(monitor, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(AuditActions.SslMonitorCreate, monitor, null, cancellationToken);

        if (monitor.IsEnabled)
            await ProbeAndRecordAsync(monitor, cancellationToken);

        return ServiceResult<Guid>.Success(monitor.Id, monitor.Status switch
        {
            SslCertificateStatus.Valid => "SSL izleme eklendi; sertifika geçerli.",
            SslCertificateStatus.Unknown => "SSL izleme eklendi.",
            _ => $"SSL izleme eklendi; durum: {StatusText(monitor.Status)}."
        });
    }

    public async Task<ServiceResult> UpdateAsync(SslMonitorFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var monitor = await _repository.GetAsync(id, cancellationToken);
        if (monitor is null)
            return ServiceResult.NotFound(NotFoundMessage);

        Normalize(dto);
        var error = await ValidateAsync(dto, monitor.Id, cancellationToken);
        if (error is not null)
            return error;

        var before = Name(monitor);
        var targetChanged = monitor.Host != dto.Host || monitor.Port != dto.Port;
        Apply(monitor, dto);
        monitor.UpdatedAt = UtcNow;
        monitor.UpdatedBy = _currentUser.UserName;
        if (targetChanged)
        {
            monitor.Status = SslCertificateStatus.Unknown;
            monitor.Subject = null;
            monitor.Issuer = null;
            monitor.NotBefore = null;
            monitor.NotAfter = null;
            monitor.ResolvedAddress = null;
            monitor.LastCheckedAt = null;
            monitor.LastError = null;
        }

        await _repository.SaveChangesAsync(cancellationToken);
        await AuditAsync(AuditActions.SslMonitorUpdate, monitor, targetChanged ? $"{before} -> {Name(monitor)}" : null, cancellationToken);

        if (targetChanged && monitor.IsEnabled)
            await ProbeAndRecordAsync(monitor, cancellationToken);

        return ServiceResult.Success("SSL izleme güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var monitor = await _repository.GetAsync(id, cancellationToken);
        if (monitor is null)
            return ServiceResult.NotFound(NotFoundMessage);

        monitor.IsDeleted = true;
        monitor.DeletedAt = UtcNow;
        monitor.DeletedBy = _currentUser.UserName;
        monitor.IsEnabled = false;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.SslMonitorDelete, monitor, null, cancellationToken);
        return ServiceResult.Success("SSL izleme silindi.");
    }

    public async Task<ServiceResult<SslMonitorListItemDto>> CheckNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var monitor = await _repository.GetAsync(id, cancellationToken);
        if (monitor is null)
            return ServiceResult<SslMonitorListItemDto>.NotFound(NotFoundMessage);

        await ProbeAndRecordAsync(monitor, cancellationToken);
        var item = ToListItem(monitor, UtcNow);
        var message = monitor.Status switch
        {
            SslCertificateStatus.Error => $"Sertifika okunamadı: {monitor.LastError}",
            SslCertificateStatus.Invalid => $"Sertifika geçersiz: {monitor.LastError}",
            _ when item.DaysRemaining is { } days => $"{StatusText(monitor.Status)} — {days} gün kaldı.",
            _ => StatusText(monitor.Status)
        };
        return ServiceResult<SslMonitorListItemDto>.Success(item, message);
    }

    public Task<IReadOnlyList<Guid>> GetDueIdsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetDueIdsAsync(UtcNow.AddHours(-Math.Max(1, _options.SslCheckIntervalHours)), DueBatchSize, cancellationToken);

    public async Task RunCheckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var monitor = await _repository.GetAsync(id, cancellationToken);
        if (monitor is null || !monitor.IsEnabled)
            return;

        await ProbeAndRecordAsync(monitor, cancellationToken);
    }

    private async Task ProbeAndRecordAsync(SslCertificateMonitor monitor, CancellationToken cancellationToken)
    {
        var probe = await _probe.ProbeAsync(monitor.Host, monitor.Port, ProbeTimeoutSeconds, cancellationToken);
        var now = UtcNow;
        monitor.LastCheckedAt = now;

        if (probe.HasCertificate)
        {
            monitor.Subject = TextHelper.Truncate(probe.Subject, 500);
            monitor.Issuer = TextHelper.Truncate(probe.Issuer, 500);
            monitor.NotBefore = probe.NotBefore;
            monitor.NotAfter = probe.NotAfter;
            monitor.ResolvedAddress = TextHelper.Truncate(probe.ResolvedAddress, 64);
            monitor.LastError = TextHelper.Truncate(probe.ValidationError, 1000);
        }
        else
        {
            monitor.LastError = TextHelper.Truncate(probe.Error ?? "Sertifika okunamadı.", 1000);
        }

        monitor.Status = SslStatusClassifier.Classify(probe, now, _options.SslExpiringDays);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<ServiceResult?> ValidateAsync(SslMonitorFormDto dto, Guid? excludeId, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        if (dto.ServerId is { } serverId && !await _serverRepository.AnyAsync(s => s.Id == serverId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.ServerId), "Seçilen sunucu bulunamadı.");

        if (await _repository.ExistsAsync(dto.Host, dto.Port, excludeId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Host), "Bu alan adı ve port zaten izleniyor.");

        return null;
    }

    private static void Normalize(SslMonitorFormDto dto)
    {
        var host = dto.Host?.Trim().ToLowerInvariant() ?? string.Empty;
        if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
        {
            host = uri.Host;
            if (!uri.IsDefaultPort && dto.Port == 443)
                dto.Port = uri.Port;
        }

        dto.Host = host.TrimEnd('.', '/');
    }

    private static void Apply(SslCertificateMonitor monitor, SslMonitorFormDto dto)
    {
        monitor.Host = dto.Host;
        monitor.Port = dto.Port;
        monitor.ServerId = dto.ServerId;
        monitor.IsEnabled = dto.IsEnabled;
    }

    public static string StatusText(SslCertificateStatus status) => status switch
    {
        SslCertificateStatus.Valid => "Geçerli",
        SslCertificateStatus.Expiring => "Süresi yaklaşıyor",
        SslCertificateStatus.Expired => "Süresi dolmuş",
        SslCertificateStatus.Invalid => "Geçersiz",
        SslCertificateStatus.Error => "Hata",
        _ => "Bilinmiyor"
    };

    private static string Name(SslCertificateMonitor monitor) =>
        monitor.Port == 443 ? monitor.Host : $"{monitor.Host}:{monitor.Port}";

    private static SslMonitorListItemDto ToListItem(SslCertificateMonitor m, DateTime now) => new(
        m.Id, m.Host, m.Port, m.ServerId, m.Server?.Name, m.IsEnabled, m.Status, m.Subject, m.Issuer, m.NotBefore, m.NotAfter,
        m.NotAfter is { } notAfter ? SslStatusClassifier.DaysRemaining(notAfter, now) : null,
        m.ResolvedAddress, m.LastCheckedAt, m.LastError);

    private Task AuditAsync(string action, SslCertificateMonitor monitor, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.SslMonitor, monitor.Id.ToString(), Name(monitor), details), cancellationToken);
}

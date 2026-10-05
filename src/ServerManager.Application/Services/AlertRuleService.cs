using FluentValidation;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class AlertRuleService : IAlertRuleService
{
    private const string NotFoundMessage = "Alarm kuralı bulunamadı.";

    private readonly IAlertRepository _repository;
    private readonly IServerRepository _serverRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<AlertRuleFormDto> _validator;
    private readonly TimeProvider _timeProvider;
    private readonly MonitoringOptions _monitoringOptions;

    public AlertRuleService(
        IAlertRepository repository,
        IServerRepository serverRepository,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<AlertRuleFormDto> validator,
        TimeProvider timeProvider,
        IOptions<MonitoringOptions> monitoringOptions)
    {
        _repository = repository;
        _serverRepository = serverRepository;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _validator = validator;
        _timeProvider = timeProvider;
        _monitoringOptions = monitoringOptions.Value;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<AlertRuleListItemDto>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _repository.GetRulesAsync(cancellationToken);
        var firing = await _repository.GetFiringCountsByRuleAsync(cancellationToken);
        return rules.Select(r => new AlertRuleListItemDto(
            r.Id,
            r.Name,
            r.Kind,
            r.Severity,
            AlertRuleKinds.Describe(r.Kind, r.Threshold, r.DurationMinutes),
            r.Server?.Name,
            r.IsEnabled,
            r.NotifyRecovery,
            r.RepeatIntervalMinutes,
            r.Channels.Where(c => c.Channel is not null).Select(c => c.Channel!.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList(),
            firing.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<ServiceResult<AlertRuleFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _repository.GetRuleAsync(id, cancellationToken);
        if (rule is null)
            return ServiceResult<AlertRuleFormDto>.NotFound(NotFoundMessage);

        return ServiceResult<AlertRuleFormDto>.Success(new AlertRuleFormDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Kind = rule.Kind,
            Severity = rule.Severity,
            Threshold = rule.Threshold,
            DurationMinutes = rule.DurationMinutes,
            ServerId = rule.ServerId,
            IsEnabled = rule.IsEnabled,
            NotifyRecovery = rule.NotifyRecovery,
            RepeatIntervalMinutes = rule.RepeatIntervalMinutes,
            ChannelIds = rule.Channels.Where(c => c.Channel is not null).Select(c => c.ChannelId).ToList()
        });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(AlertRuleFormDto dto, CancellationToken cancellationToken = default)
    {
        Normalize(dto);
        var error = await ValidateAsync(dto, null, cancellationToken);
        if (error is not null)
            return ServiceResult<Guid>.ValidationFailure(error.Errors);

        var rule = new AlertRule { CreatedAt = UtcNow, CreatedBy = _currentUser.UserName };
        Apply(rule, dto);
        var channelIds = await _repository.GetExistingChannelIdsAsync(dto.ChannelIds, cancellationToken);
        foreach (var channelId in channelIds)
            rule.Channels.Add(new AlertRuleChannel { RuleId = rule.Id, ChannelId = channelId });

        await _repository.AddRuleAsync(rule, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.AlertRuleCreate, rule, $"{AlertRuleKinds.DisplayName(rule.Kind)} | {AlertRuleKinds.Describe(rule.Kind, rule.Threshold, rule.DurationMinutes)} | Kanal: {channelIds.Count}", cancellationToken);
        return ServiceResult<Guid>.Success(rule.Id, "Alarm kuralı eklendi.");
    }

    public async Task<ServiceResult> UpdateAsync(AlertRuleFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var rule = await _repository.GetRuleAsync(id, cancellationToken);
        if (rule is null)
            return ServiceResult.NotFound(NotFoundMessage);

        Normalize(dto);
        var error = await ValidateAsync(dto, rule.Id, cancellationToken);
        if (error is not null)
            return error;

        var before = AlertRuleKinds.Describe(rule.Kind, rule.Threshold, rule.DurationMinutes);
        Apply(rule, dto);
        rule.UpdatedAt = UtcNow;
        rule.UpdatedBy = _currentUser.UserName;

        var channelIds = (await _repository.GetExistingChannelIdsAsync(dto.ChannelIds, cancellationToken)).ToHashSet();
        var removed = rule.Channels.Where(c => !channelIds.Contains(c.ChannelId)).ToList();
        _repository.RemoveRuleChannels(removed);
        foreach (var link in removed)
            rule.Channels.Remove(link);
        foreach (var channelId in channelIds.Where(cid => rule.Channels.All(c => c.ChannelId != cid)))
            rule.Channels.Add(new AlertRuleChannel { RuleId = rule.Id, ChannelId = channelId });

        await _repository.SaveChangesAsync(cancellationToken);

        var after = AlertRuleKinds.Describe(rule.Kind, rule.Threshold, rule.DurationMinutes);
        var details = before == after ? after : $"{before} -> {after}";
        await AuditAsync(AuditActions.AlertRuleUpdate, rule, $"{details} | Kanal: {channelIds.Count} | {(rule.IsEnabled ? "Etkin" : "Kapalı")}", cancellationToken);
        return ServiceResult.Success("Alarm kuralı güncellendi.");
    }

    public async Task<ServiceResult> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var rule = await _repository.GetRuleAsync(id, cancellationToken);
        if (rule is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (rule.IsEnabled == enabled)
            return ServiceResult.Success(enabled ? "Kural zaten etkin." : "Kural zaten kapalı.");

        rule.IsEnabled = enabled;
        rule.UpdatedAt = UtcNow;
        rule.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.AlertRuleUpdate, rule, enabled ? "Etkinleştirildi" : "Devre dışı bırakıldı", cancellationToken);
        return ServiceResult.Success(enabled
            ? "Kural etkinleştirildi."
            : "Kural devre dışı bırakıldı; açık alarmları bir sonraki değerlendirmede kapatılır.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _repository.GetRuleAsync(id, cancellationToken);
        if (rule is null)
            return ServiceResult.NotFound(NotFoundMessage);

        rule.IsDeleted = true;
        rule.DeletedAt = UtcNow;
        rule.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.AlertRuleDelete, rule, AlertRuleKinds.Describe(rule.Kind, rule.Threshold, rule.DurationMinutes), cancellationToken);
        return ServiceResult.Success("Alarm kuralı silindi; geçmiş alarmlar korunur.");
    }

    public async Task<IReadOnlyList<ServerOptionDto>> GetServerOptionsAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.FindAsync(_ => true, cancellationToken);
        return servers
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ServerOptionDto(s.Id, s.Name, s.IpAddress))
            .ToList();
    }

    private async Task<ServiceResult?> ValidateAsync(AlertRuleFormDto dto, Guid? excludeId, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        if (DurationExceedsRetention(dto.Kind, dto.DurationMinutes, _monitoringOptions))
        {
            return ServiceResult.ValidationFailure(nameof(dto.DurationMinutes),
                $"Süre, ham metrik saklama süresini ({_monitoringOptions.EffectiveRawRetentionHours} saat) aşamaz; bu kural hiç tetiklenmez. " +
                "Süreyi kısaltın veya Ayarlar'dan ham metrik saklama süresini artırın.");
        }

        if (dto.ServerId is { } serverId && !await _serverRepository.AnyAsync(s => s.Id == serverId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.ServerId), "Seçilen sunucu bulunamadı.");

        if (await _repository.RuleNameExistsAsync(dto.Name, excludeId, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir kural zaten kayıtlı.");

        return null;
    }

    /// <summary>Metrik kuralının penceresi ham örneklerin saklandığı süreden uzunsa pencere hiçbir zaman dolmaz.</summary>
    public static bool DurationExceedsRetention(AlertRuleKind kind, int durationMinutes, MonitoringOptions options) =>
        AlertRuleKinds.IsMetric(kind) && durationMinutes > options.EffectiveRawRetentionHours * 60;

    private static void Normalize(AlertRuleFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.ChannelIds = dto.ChannelIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (!AlertRuleKinds.UsesThreshold(dto.Kind))
            dto.Threshold = 0;
        if (!AlertRuleKinds.UsesDuration(dto.Kind))
            dto.DurationMinutes = 0;
    }

    private static void Apply(AlertRule rule, AlertRuleFormDto dto)
    {
        rule.Name = dto.Name;
        rule.Kind = dto.Kind;
        rule.Severity = dto.Severity;
        rule.Threshold = dto.Threshold;
        rule.DurationMinutes = dto.DurationMinutes;
        rule.ServerId = dto.ServerId;
        rule.IsEnabled = dto.IsEnabled;
        rule.NotifyRecovery = dto.NotifyRecovery;
        rule.RepeatIntervalMinutes = dto.RepeatIntervalMinutes;
    }

    private Task AuditAsync(string action, AlertRule rule, string? details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(action, AuditEntityTypes.AlertRule, rule.Id.ToString(), rule.Name, details), cancellationToken);
}

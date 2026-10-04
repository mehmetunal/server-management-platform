using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Alerting;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Cloud;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Settings;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Security;
using ServerManager.Application.Settings;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class PanelSettingsService : IPanelSettingsService
{
    private readonly IPanelSettingRepository _repository;
    private readonly IAuditLogService _auditLogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PanelSettingsService> _logger;
    private readonly Dictionary<string, object> _targets;

    public PanelSettingsService(
        IPanelSettingRepository repository,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        ILogger<PanelSettingsService> logger,
        IOptions<MonitoringOptions> monitoring,
        IOptions<AlertingOptions> alerting,
        IOptions<BackupOptions> backup,
        IOptions<SecurityScanOptions> securityScan,
        IOptions<CloudOptions> cloud)
    {
        _repository = repository;
        _auditLogService = auditLogService;
        _timeProvider = timeProvider;
        _logger = logger;
        _targets = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [MonitoringOptions.SectionName] = monitoring.Value,
            [AlertingOptions.SectionName] = alerting.Value,
            [BackupOptions.SectionName] = backup.Value,
            [SecurityScanOptions.SectionName] = securityScan.Value,
            [CloudOptions.SectionName] = cloud.Value
        };
    }

    public IReadOnlyList<PanelSettingGroupDto> GetGroups() =>
        PanelSettingCatalog.All
            .GroupBy(d => d.Section)
            .Select(group => new PanelSettingGroupDto(group.Key, group.Select(ToField).ToList()))
            .ToList();

    public async Task ApplyStoredAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAllAsync(cancellationToken);
        foreach (var setting in stored)
        {
            if (!PanelSettingCatalog.TryGet(setting.Key, out var definition))
                continue;

            if (!TryParse(definition, setting.Value, out var parsed, out _))
            {
                _logger.LogWarning("Kayıtlı panel ayarı uygulanamadı. Key: {Key}", setting.Key);
                continue;
            }

            Write(definition, parsed);
        }
    }

    public async Task<ServiceResult> SaveAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        var errors = new List<ServiceError>();
        var parsed = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var definition in PanelSettingCatalog.All)
        {
            if (!values.TryGetValue(definition.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                errors.Add(new ServiceError(definition.Key, $"{definition.Label} gerekli."));
                continue;
            }

            if (!TryParse(definition, raw, out var value, out var message))
                errors.Add(new ServiceError(definition.Key, message!));
            else
                parsed[definition.Key] = value;
        }

        foreach (var (warningKey, criticalKey) in PanelSettingCatalog.ThresholdPairs)
        {
            if (parsed.TryGetValue(warningKey, out var warning) && parsed.TryGetValue(criticalKey, out var critical)
                && Convert.ToDouble(critical, CultureInfo.InvariantCulture) <= Convert.ToDouble(warning, CultureInfo.InvariantCulture))
            {
                var label = PanelSettingCatalog.All.First(d => d.Key == criticalKey).Label;
                errors.Add(new ServiceError(criticalKey, $"{label}, uyarı eşiğinden büyük olmalı."));
            }
        }

        if (errors.Count > 0)
            return ServiceResult.ValidationFailure(errors);

        var changes = new List<string>();
        var rows = new List<PanelSetting>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var definition in PanelSettingCatalog.All)
        {
            var next = Format(definition, parsed[definition.Key]);
            var current = Read(definition);
            if (!string.Equals(current, next, StringComparison.Ordinal))
            {
                changes.Add($"{definition.Label}: {Display(definition, current)} → {Display(definition, next)}");
                rows.Add(new PanelSetting { Key = definition.Key, Value = next, UpdatedAt = now });
            }
        }

        if (rows.Count == 0)
            return ServiceResult.Success("Değişiklik yok.");

        await _repository.UpsertAsync(rows, cancellationToken);
        foreach (var definition in PanelSettingCatalog.All)
            Write(definition, parsed[definition.Key]);

        var details = string.Join(" | ", changes);
        if (details.Length > 2000)
            details = details[..2000];

        await _auditLogService.LogAsync(
            new AuditEntry(AuditActions.SettingsUpdate, AuditEntityTypes.Settings, "panel", "Ayarlar", details),
            cancellationToken);

        return ServiceResult.Success("Ayarlar kaydedildi. Çalışan işler yeni değeri bir sonraki turda kullanır.");
    }

    private PanelSettingFieldDto ToField(PanelSettingDefinition definition) =>
        new(definition.Key, definition.Label, definition.Kind.ToString(), Read(definition), definition.Hint,
            definition.Kind is PanelSettingKind.Integer or PanelSettingKind.Number ? definition.Minimum : null,
            definition.Kind is PanelSettingKind.Integer or PanelSettingKind.Number ? definition.Maximum : null,
            definition.Cluster);

    private string Read(PanelSettingDefinition definition) => Format(definition, Property(definition).GetValue(Target(definition)));

    private void Write(PanelSettingDefinition definition, object? value) => Property(definition).SetValue(Target(definition), value);

    private object Target(PanelSettingDefinition definition)
    {
        var section = definition.Key.Split(':')[0];
        return _targets[section];
    }

    private static System.Reflection.PropertyInfo Property(PanelSettingDefinition definition)
    {
        var name = definition.Key.Split(':')[1];
        return TargetType(definition).GetProperty(name)
            ?? throw new InvalidOperationException($"Ayar özelliği bulunamadı: {definition.Key}");
    }

    private static Type TargetType(PanelSettingDefinition definition) => definition.Key.Split(':')[0] switch
    {
        MonitoringOptions.SectionName => typeof(MonitoringOptions),
        AlertingOptions.SectionName => typeof(AlertingOptions),
        BackupOptions.SectionName => typeof(BackupOptions),
        SecurityScanOptions.SectionName => typeof(SecurityScanOptions),
        CloudOptions.SectionName => typeof(CloudOptions),
        _ => throw new InvalidOperationException($"Ayar bölümü yok: {definition.Key}")
    };

    private static bool TryParse(PanelSettingDefinition definition, string raw, out object? value, out string? error)
    {
        raw = raw.Trim();
        value = null;
        error = null;

        switch (definition.Kind)
        {
            case PanelSettingKind.Boolean:
                if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
                    value = true;
                else if (raw.Equals("false", StringComparison.OrdinalIgnoreCase))
                    value = false;
                else
                    error = $"{definition.Label} açık veya kapalı olmalı.";
                return error is null;

            case PanelSettingKind.Integer:
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                    || integer < definition.Minimum || integer > definition.Maximum)
                {
                    error = $"{definition.Label} {definition.Minimum:0} ile {definition.Maximum:0} arasında olmalı.";
                    return false;
                }

                value = integer;
                return true;

            case PanelSettingKind.Number:
                var normalized = raw.Replace(',', '.');
                if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || number < definition.Minimum || number > definition.Maximum)
                {
                    error = $"{definition.Label} {definition.Minimum:0} ile {definition.Maximum:0} arasında olmalı.";
                    return false;
                }

                value = number;
                return true;

            case PanelSettingKind.TimeZone:
                if (raw.Length is < 1 or > 64 || !TimeZoneInfo.TryFindSystemTimeZoneById(raw, out _))
                {
                    error = $"{definition.Label} tanınmıyor. Örnek: Europe/Istanbul.";
                    return false;
                }

                value = raw;
                return true;

            default:
                error = $"{definition.Label} okunamadı.";
                return false;
        }
    }

    private static string Format(PanelSettingDefinition definition, object? value) => definition.Kind switch
    {
        PanelSettingKind.Boolean => value is true ? "true" : "false",
        PanelSettingKind.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        PanelSettingKind.Number => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static string Display(PanelSettingDefinition definition, string value) =>
        definition.Kind == PanelSettingKind.Boolean ? (value == "true" ? "Açık" : "Kapalı") : value;
}

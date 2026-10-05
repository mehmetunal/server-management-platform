using System.Text.RegularExpressions;
using ServerManager.Application.Monitoring;
using ServerManager.Domain.Enums;

namespace ServerManager.Infrastructure.Repositories;

internal enum RetentionMode
{
    /// <summary>Süresi dolan satırlar silinir; bölüm başına en yeni N satır korunur.</summary>
    DeleteRows = 1,

    /// <summary>Satır kalır, yalnızca log kolonu kısa bir açıklamayla değiştirilir; bölüm başına en yeni N satır korunur.</summary>
    TrimLog = 2,

    /// <summary>Üst kayıt alt kayıtlarıyla (FK) birlikte silinir.</summary>
    DeleteWithChildren = 3
}

/// <param name="PartitionColumn">Null ise en yeni satır koruması yoktur.</param>
/// <param name="Filter">Kodda sabit ek koşul ("t." önekli); kullanıcı girdisi içermez.</param>
internal sealed record RetentionMapping(
    string Table,
    string TimeColumn,
    string? PartitionColumn,
    RetentionMode Mode = RetentionMode.DeleteRows,
    string? Filter = null,
    string? LogColumn = null,
    string? ChildTable = null,
    string? ChildForeignKey = null);

/// <summary>
/// Otomatik silme yalnızca geçici izleme/tarama tablolarında ve panelden saklama süresi verilen geçmiş/log tablolarında
/// çalışabilir; kullanıcı, sunucu ve denetim (AuditLogs) verisi asla silinmez.
/// </summary>
internal static class RetentionAllowList
{
    private static readonly Regex AllowedTablePattern = new(
        "^(ServerMetrics|ServerMetricsHourly|ServerHealthChecks|UptimeCheckResults|NotificationDeliveries|SecurityScans" +
        "|Deployments|BackupRuns|CommandRuns|CommandRunTargets|TerminalSessions|TerminalCommands|AlertEvents" +
        "|ManagedServiceOperations)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedColumnPattern = new(
        "^(CollectedAt|HourStart|CheckedAt|SentAt|StartedAt|EndedAt|ResolvedAt)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedPartitionPattern = new(
        "^(ServerId|CheckId|ChannelId|ProjectId|JobId|ServiceId)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedLogColumnPattern = new("^Log$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AllowedForeignKeyPattern = new("^(RunId|SessionId)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<RetentionTarget, RetentionMapping> Targets =
        new Dictionary<RetentionTarget, RetentionMapping>
        {
            [RetentionTarget.RawMetrics] = new("ServerMetrics", "CollectedAt", "ServerId"),
            [RetentionTarget.HourlyMetrics] = new("ServerMetricsHourly", "HourStart", "ServerId"),
            [RetentionTarget.HealthChecks] = new("ServerHealthChecks", "CheckedAt", "ServerId"),
            [RetentionTarget.UptimeResults] = new("UptimeCheckResults", "CheckedAt", "CheckId"),
            [RetentionTarget.NotificationDeliveries] = new("NotificationDeliveries", "SentAt", "ChannelId"),
            [RetentionTarget.SecurityScans] = new("SecurityScans", "StartedAt", "ServerId"),
            [RetentionTarget.DeploymentLogs] = new("Deployments", "StartedAt", "ProjectId", RetentionMode.TrimLog,
                Filter: "t.CompletedAt IS NOT NULL", LogColumn: "Log"),
            [RetentionTarget.BackupRunLogs] = new("BackupRuns", "StartedAt", "JobId", RetentionMode.TrimLog,
                Filter: "t.CompletedAt IS NOT NULL", LogColumn: "Log"),
            [RetentionTarget.CommandRuns] = new("CommandRuns", "StartedAt", null, RetentionMode.DeleteWithChildren,
                Filter: "t.CompletedAt IS NOT NULL", ChildTable: "CommandRunTargets", ChildForeignKey: "RunId"),
            [RetentionTarget.TerminalSessions] = new("TerminalSessions", "EndedAt", null, RetentionMode.DeleteWithChildren,
                ChildTable: "TerminalCommands", ChildForeignKey: "SessionId"),
            [RetentionTarget.AlertEvents] = new("AlertEvents", "ResolvedAt", null, Filter: $"t.Status = {(int)AlertEventStatus.Resolved}"),
            [RetentionTarget.ServiceOperationLogs] = new("ManagedServiceOperations", "StartedAt", "ServiceId", RetentionMode.TrimLog,
                Filter: "t.FinishedAt IS NOT NULL", LogColumn: "Log")
        };

    public static RetentionMapping Resolve(RetentionTarget target)
    {
        if (!Targets.TryGetValue(target, out var mapping))
            throw new InvalidOperationException($"Bilinmeyen saklama hedefi: {target}. Silme işlemi durduruldu.");

        EnsureAllowed(mapping.Table, mapping.TimeColumn, mapping.PartitionColumn ?? "ServerId");
        if (mapping.LogColumn is not null && !AllowedLogColumnPattern.IsMatch(mapping.LogColumn))
            throw new InvalidOperationException($"'{mapping.LogColumn}' kolonu temizleme izin listesinde değil. İşlem durduruldu.");
        if (mapping.ChildTable is not null)
        {
            if (mapping.ChildForeignKey is null || !AllowedForeignKeyPattern.IsMatch(mapping.ChildForeignKey))
                throw new InvalidOperationException($"'{mapping.ChildForeignKey}' kolonu silme izin listesinde değil. Silme işlemi durduruldu.");
            if (!AllowedTablePattern.IsMatch(mapping.ChildTable))
                throw new InvalidOperationException($"'{mapping.ChildTable}' tablosu silme izin listesinde değil. Silme işlemi durduruldu.");
        }

        return mapping;
    }

    public static void EnsureAllowed(string table, string timeColumn, string partitionColumn = "ServerId")
    {
        if (!AllowedTablePattern.IsMatch(table))
            throw new InvalidOperationException($"'{table}' tablosu silme izin listesinde değil. Silme işlemi durduruldu.");

        if (!AllowedColumnPattern.IsMatch(timeColumn))
            throw new InvalidOperationException($"'{timeColumn}' kolonu silme izin listesinde değil. Silme işlemi durduruldu.");

        if (!AllowedPartitionPattern.IsMatch(partitionColumn))
            throw new InvalidOperationException($"'{partitionColumn}' kolonu silme izin listesinde değil. Silme işlemi durduruldu.");
    }
}

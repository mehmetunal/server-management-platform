using System.Globalization;
using ServerManager.Application.Auditing;
using ServerManager.Application.Cleanup;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Services;

public sealed class ServerCleanupService : IServerCleanupService
{
    private const int MaxAuditDetails = 2000;

    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IServerCleanupInspector _inspector;
    private readonly IDeploymentRepository _deployments;
    private readonly IManagedServiceRepository _services;
    private readonly IAuditLogService _auditLogService;

    public ServerCleanupService(
        IServerConnectionProvider connectionProvider,
        IServerCleanupInspector inspector,
        IDeploymentRepository deployments,
        IManagedServiceRepository services,
        IAuditLogService auditLogService)
    {
        _connectionProvider = connectionProvider;
        _inspector = inspector;
        _deployments = deployments;
        _services = services;
        _auditLogService = auditLogService;
    }

    public async Task<ServiceResult<CleanupScan>> ScanAsync(Guid serverId, CleanupOptions options, CancellationToken cancellationToken = default)
    {
        options = CleanupRules.Normalize(options);
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<CleanupScan>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var facts = await _inspector.ScanAsync(connection.Data.Context, options, cancellationToken);
        if (!facts.IsSuccess || facts.Data is null)
            return ServiceResult<CleanupScan>.Failure(facts.Message ?? "Sunucu taranamadı.");

        var context = await PanelResourceContextLoader.LoadAsync(_deployments, _services, serverId, cancellationToken);
        return ServiceResult<CleanupScan>.Success(BuildScan(facts.Data, context, options, connection.Data.Context.UseSudo));
    }

    public async Task<ServiceResult<CleanupRunResult>> ExecuteAsync(
        Guid serverId,
        IReadOnlyCollection<string> keys,
        CleanupOptions options,
        bool dryRun,
        Func<CleanupLogEntry, CancellationToken, Task> onLog,
        CancellationToken cancellationToken = default)
    {
        options = CleanupRules.Normalize(options);
        var selected = keys.Where(CleanupRules.IsValidKey).ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0)
            return ServiceResult<CleanupRunResult>.Failure("Temizlenecek öğe seçilmedi.");
        if (selected.Count > CleanupRules.MaxSelectedItems)
            return ServiceResult<CleanupRunResult>.Failure($"Tek seferde en fazla {CleanupRules.MaxSelectedItems} öğe temizlenebilir.");

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<CleanupRunResult>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        await onLog(new CleanupLogEntry(CleanupLogLevel.Info, "Sunucu yeniden taranıyor; yalnızca hâlâ silinebilir olan öğeler işlenecek."), cancellationToken);
        var facts = await _inspector.ScanAsync(connection.Data.Context, options, cancellationToken);
        if (!facts.IsSuccess || facts.Data is null)
            return ServiceResult<CleanupRunResult>.Failure(facts.Message ?? "Sunucu taranamadı.");

        var context = await PanelResourceContextLoader.LoadAsync(_deployments, _services, serverId, cancellationToken);
        var scan = BuildScan(facts.Data, context, options, connection.Data.Context.UseSudo);
        var items = scan.Items.Where(i => i.CanDelete && selected.Contains(i.Key)).ToList();
        var missing = selected.Count - items.Count;
        if (missing > 0)
        {
            await onLog(new CleanupLogEntry(CleanupLogLevel.Warning,
                $"{missing.ToString(CultureInfo.InvariantCulture)} öğe artık silinebilir durumda değil (silinmiş, kullanıma girmiş veya korumalı); atlandı."), cancellationToken);
        }

        if (items.Count == 0)
            return ServiceResult<CleanupRunResult>.Failure("Seçilen öğelerin hiçbiri artık silinebilir durumda değil. Listeyi yenileyin.");

        var run = await _inspector.ExecuteAsync(connection.Data.Context, items, options, dryRun, onLog, cancellationToken);
        if (!run.IsSuccess || run.Data is null)
            return ServiceResult<CleanupRunResult>.Failure(run.Message ?? "Temizlik çalıştırılamadı.");

        var result = run.Data with { Skipped = run.Data.Skipped + missing };
        if (!dryRun)
        {
            await _auditLogService.LogAsync(new AuditEntry(
                AuditActions.ServerCleanup,
                AuditEntityTypes.Server,
                serverId.ToString(),
                connection.Data.ServerName,
                AuditDetails(result),
                result.Failed == 0), cancellationToken);
        }

        return ServiceResult<CleanupRunResult>.Success(result, Summary(result));
    }

    internal static CleanupScan BuildScan(CleanupFacts facts, PanelResourceContext context, CleanupOptions options, bool usesSudo) =>
        new(CleanupClassifier.Classify(facts, context, options), options, facts.DockerAvailable, facts.DockerMessage,
            facts.PackageManager, facts.JournalBytes, usesSudo);

    internal static string Summary(CleanupRunResult result)
    {
        if (result.DryRun)
            return $"Önizleme tamamlandı: {result.Succeeded.ToString(CultureInfo.InvariantCulture)} öğe incelendi, hiçbir şey silinmedi. Tahmini kazanç: {ByteSize.Format(result.EstimatedBytes)}.";

        var freed = result.FreedBytes is { } bytes ? ByteSize.Format(Math.Max(0, bytes)) : "ölçülemedi";
        return result.Failed == 0
            ? $"Temizlik tamamlandı: {result.Succeeded.ToString(CultureInfo.InvariantCulture)} öğe silindi. Kazanılan alan: {freed}."
            : $"Temizlik bitti: {result.Succeeded.ToString(CultureInfo.InvariantCulture)} başarılı, {result.Failed.ToString(CultureInfo.InvariantCulture)} hatalı. Kazanılan alan: {freed}.";
    }

    private static string AuditDetails(CleanupRunResult result)
    {
        var freed = result.FreedBytes is { } bytes
            ? $"{ByteSize.Format(Math.Max(0, bytes))} ({Math.Max(0, bytes).ToString(CultureInfo.InvariantCulture)} bayt)"
            : "ölçülemedi";
        var header = $"{result.Succeeded.ToString(CultureInfo.InvariantCulture)} başarılı, {result.Failed.ToString(CultureInfo.InvariantCulture)} hatalı, " +
                     $"{result.Skipped.ToString(CultureInfo.InvariantCulture)} atlandı. Kazanılan alan: {freed}. Öğeler: ";
        return TextHelper.Truncate(header + string.Join(", ", result.ProcessedItems), MaxAuditDetails)!;
    }
}

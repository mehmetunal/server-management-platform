using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Monitoring;

namespace ServerManager.Infrastructure.Repositories;

internal static class RetentionDeleter
{
    public const int DeleteBatchSize = 5000;

    /// <summary>Log kırpma ve alt kayıtlı silmede satırlar büyük olduğundan daha küçük partiler kullanılır.</summary>
    public const int HeavyBatchSize = 500;

    /// <summary>Bakım komutları büyük tablolarda varsayılan 30 sn'yi aşabilir.</summary>
    public static readonly TimeSpan MaintenanceCommandTimeout = TimeSpan.FromMinutes(5);

    private const string KeepTable = "#RetentionKeep";

    /// <summary>
    /// Süresi dolan kayıtları partiler halinde siler (veya log kolonunu kırpar). Her bölümün en yeni kayıtları süresi dolmuş
    /// olsa bile korunur; korunan kimlikler çalıştırma başına bir kez geçici tabloya yazılır, her partide yeniden hesaplanmaz.
    /// </summary>
    public static async Task<int> DeleteExpiredAsync(DbContext context, RetentionTarget target, DateTime cutoffUtc, int keepLatestPerPartition, CancellationToken cancellationToken)
    {
        var mapping = RetentionAllowList.Resolve(target);
        var keep = Math.Max(1, keepLatestPerPartition);
        var database = context.Database;
        var previousTimeout = database.GetCommandTimeout();

        // Geçici tablo oturuma bağlıdır; tüm komutlar aynı bağlantıda çalışmalı.
        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            database.SetCommandTimeout(MaintenanceCommandTimeout);

            var protect = mapping.PartitionColumn is not null && mapping.Mode != RetentionMode.DeleteWithChildren;
            if (protect)
            {
                await database.ExecuteSqlRawAsync(BuildKeepSql(mapping),
                    [new SqlParameter("@keep", SqlDbType.Int) { Value = keep }], cancellationToken);
            }

            return mapping.Mode switch
            {
                RetentionMode.TrimLog => await TrimLogsAsync(context, mapping, cutoffUtc, protect, cancellationToken),
                RetentionMode.DeleteWithChildren => await DeleteWithChildrenAsync(context, mapping, cutoffUtc, cancellationToken),
                _ => await DeleteRowsAsync(context, mapping, cutoffUtc, protect, cancellationToken)
            };
        }
        finally
        {
            try
            {
                await database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {KeepTable};", CancellationToken.None);
            }
            catch (Exception)
            {
                // Bağlantı kapanınca geçici tablo zaten düşer.
            }

            database.SetCommandTimeout(previousTimeout);
            await database.CloseConnectionAsync();
        }
    }

    internal static string BuildKeepSql(RetentionMapping mapping) => $"""
        DROP TABLE IF EXISTS {KeepTable};
        SELECT r.Id INTO {KeepTable} FROM (
            SELECT Id, ROW_NUMBER() OVER (PARTITION BY {mapping.PartitionColumn} ORDER BY {mapping.TimeColumn} DESC) AS RowNumber
            FROM {mapping.Table}) r
        WHERE r.RowNumber <= @keep;
        CREATE UNIQUE CLUSTERED INDEX IX_RetentionKeep_Id ON {KeepTable} (Id);
        """;

    internal static string BuildDeleteSql(RetentionMapping mapping, bool protect) => $"""
        DELETE TOP (@batch) t FROM {mapping.Table} t
        WHERE t.{mapping.TimeColumn} < @cutoff{Filter(mapping)}{Protection(protect)};
        """;

    internal static string BuildTrimSql(RetentionMapping mapping, bool protect) => $"""
        UPDATE TOP (@batch) t SET t.{mapping.LogColumn} = @marker
        FROM {mapping.Table} t
        WHERE t.{mapping.TimeColumn} < @cutoff{Filter(mapping)}
          AND DATALENGTH(t.{mapping.LogColumn}) > DATALENGTH(@marker){Protection(protect)};
        """;

    internal static string BuildDeleteWithChildrenSql(RetentionMapping mapping) => $"""
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        DECLARE @ids TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @ids (Id)
            SELECT TOP (@batch) t.Id FROM {mapping.Table} t
            WHERE t.{mapping.TimeColumn} < @cutoff{Filter(mapping)};
        DELETE c FROM {mapping.ChildTable} c WHERE c.{mapping.ChildForeignKey} IN (SELECT Id FROM @ids);
        DELETE t FROM {mapping.Table} t WHERE t.Id IN (SELECT Id FROM @ids);
        SET @deleted = @@ROWCOUNT;
        COMMIT TRANSACTION;
        """;

    private static async Task<int> DeleteRowsAsync(DbContext context, RetentionMapping mapping, DateTime cutoffUtc, bool protect, CancellationToken cancellationToken)
    {
        var sql = BuildDeleteSql(mapping, protect);
        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleted = await context.Database.ExecuteSqlRawAsync(sql,
            [
                new SqlParameter("@batch", SqlDbType.Int) { Value = DeleteBatchSize },
                new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = cutoffUtc }
            ], cancellationToken);

            total += deleted;
            if (deleted < DeleteBatchSize)
                return total;
        }
    }

    private static async Task<int> TrimLogsAsync(DbContext context, RetentionMapping mapping, DateTime cutoffUtc, bool protect, CancellationToken cancellationToken)
    {
        var sql = BuildTrimSql(mapping, protect);
        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trimmed = await context.Database.ExecuteSqlRawAsync(sql,
            [
                new SqlParameter("@batch", SqlDbType.Int) { Value = HeavyBatchSize },
                new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = cutoffUtc },
                new SqlParameter("@marker", SqlDbType.NVarChar, -1) { Value = RetentionOptions.TrimmedLogMarker }
            ], cancellationToken);

            total += trimmed;
            if (trimmed < HeavyBatchSize)
                return total;
        }
    }

    private static async Task<int> DeleteWithChildrenAsync(DbContext context, RetentionMapping mapping, DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var sql = BuildDeleteWithChildrenSql(mapping);
        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deletedParameter = new SqlParameter("@deleted", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await context.Database.ExecuteSqlRawAsync(sql,
            [
                new SqlParameter("@batch", SqlDbType.Int) { Value = HeavyBatchSize },
                new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = cutoffUtc },
                deletedParameter
            ], cancellationToken);

            var deleted = deletedParameter.Value is int value ? value : 0;
            total += deleted;
            if (deleted < HeavyBatchSize)
                return total;
        }
    }

    private static string Filter(RetentionMapping mapping) =>
        mapping.Filter is null ? string.Empty : $"\n  AND {mapping.Filter}";

    private static string Protection(bool protect) =>
        protect ? $"\n  AND NOT EXISTS (SELECT 1 FROM {KeepTable} k WHERE k.Id = t.Id)" : string.Empty;
}

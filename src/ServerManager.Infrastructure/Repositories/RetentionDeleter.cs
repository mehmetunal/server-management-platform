using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Monitoring;

namespace ServerManager.Infrastructure.Repositories;

internal static class RetentionDeleter
{
    public const int DeleteBatchSize = 5000;

    public static async Task<int> DeleteExpiredAsync(DbContext context, RetentionTarget target, DateTime cutoffUtc, int keepLatestPerPartition, CancellationToken cancellationToken)
    {
        var (table, timeColumn, partitionColumn) = RetentionAllowList.Resolve(target);
        var keep = Math.Max(1, keepLatestPerPartition);

        // Her hedefin (sunucu, kontrol, kanal) en yeni kayıtları süresi dolmuş olsa bile korunur.
        var sql = $"""
            DELETE TOP (@batch) t FROM {table} t
            WHERE t.{timeColumn} < @cutoff
              AND t.Id NOT IN (
                  SELECT r.Id FROM (
                      SELECT Id, ROW_NUMBER() OVER (PARTITION BY {partitionColumn} ORDER BY {timeColumn} DESC) AS RowNumber
                      FROM {table}) r
                  WHERE r.RowNumber <= @keep);
            """;

        var total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleted = await context.Database.ExecuteSqlRawAsync(sql,
            [
                new SqlParameter("@batch", SqlDbType.Int) { Value = DeleteBatchSize },
                new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = cutoffUtc },
                new SqlParameter("@keep", SqlDbType.Int) { Value = keep }
            ], cancellationToken);

            total += deleted;
            if (deleted < DeleteBatchSize)
                break;
        }

        return total;
    }
}

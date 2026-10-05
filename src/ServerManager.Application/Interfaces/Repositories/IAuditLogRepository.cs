using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<PagedResult<AuditLog>> SearchAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    Task<AuditLog?> GetDetailsAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Filtreye uyan en yeni <paramref name="maxRows"/> kayıt ve toplam eşleşme sayısı.</summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ExportAsync(AuditLogFilterDto filter, int maxRows, CancellationToken cancellationToken = default);

    Task<AuditStatsDto> GetStatsAsync(DateTime sinceUtc, string failedLoginAction, int topActionCount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>En büyük Id'li kaydın zincir imzası (kayıt yoksa veya imzasızsa null).</summary>
    Task<string?> GetLastChainHashAsync(CancellationToken cancellationToken = default);

    /// <summary>Id'si <paramref name="afterId"/>'den büyük kayıtlar, Id sırasıyla.</summary>
    Task<IReadOnlyList<AuditLog>> GetChainBatchAsync(long afterId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="action"/>'ı bir transaction ve veritabanı düzeyinde zincir kilidi (sp_getapplock) altında çalıştırır;
    /// birden fazla uygulama örneği aynı anda yazsa da zincir çatallanmaz. Geçici hatada işlem baştan tekrarlanabilir.
    /// </summary>
    Task RunInChainLockAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<AuditChainAnchor?> GetChainAnchorAsync(CancellationToken cancellationToken = default);

    /// <summary>Çapayı ekler ya da günceller (değişiklik izlenmez, hemen yazılır).</summary>
    Task SaveChainAnchorAsync(AuditChainAnchor anchor, CancellationToken cancellationToken = default);

    /// <summary>İmzalı kayıt yoksa null.</summary>
    Task<AuditChainSummary?> GetChainSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Kaydedilemeyen kaydı izlemeden çıkarır; aksi halde paylaşılan context'in sonraki SaveChanges çağrısı onu eski imzayla yazar.</summary>
    void Detach(AuditLog log);
}

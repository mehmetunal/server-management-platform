using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

/// <summary>Proje ↔ yönetilen servis bağları. Silinmiş proje veya kaldırılmış servise ait bağlar dönmez.</summary>
public interface IProjectServiceLinkRepository
{
    /// <summary>Servis bilgisiyle (ve sunucusuyla) birlikte.</summary>
    Task<IReadOnlyList<ProjectServiceLink>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Proje bilgisiyle birlikte.</summary>
    Task<IReadOnlyList<ProjectServiceLink>> ListByServiceAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task<ProjectServiceLink?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Silinmiş/kaldırılmış kayıtlara ait bağlar dahil (benzersiz indeks için).</summary>
    Task<ProjectServiceLink?> FindAnyAsync(Guid projectId, Guid serviceId, CancellationToken cancellationToken = default);

    Task AddAsync(ProjectServiceLink link, CancellationToken cancellationToken = default);

    void Remove(ProjectServiceLink link);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

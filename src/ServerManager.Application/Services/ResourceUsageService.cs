using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ResourceUsage;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Services;

public sealed class ResourceUsageService : IResourceUsageService
{
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IResourceUsageInspector _inspector;
    private readonly IDeploymentRepository _deployments;
    private readonly IManagedServiceRepository _services;
    private readonly TimeProvider _timeProvider;

    public ResourceUsageService(
        IServerConnectionProvider connectionProvider,
        IResourceUsageInspector inspector,
        IDeploymentRepository deployments,
        IManagedServiceRepository services,
        TimeProvider timeProvider)
    {
        _connectionProvider = connectionProvider;
        _inspector = inspector;
        _deployments = deployments;
        _services = services;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<ResourceOverview>> GetOverviewAsync(Guid serverId, bool includeDocker, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<ResourceOverview>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var facts = await _inspector.GetFactsAsync(connection.Data.Context, includeDocker, cancellationToken);
        if (!facts.IsSuccess || facts.Data is null)
            return ServiceResult<ResourceOverview>.Failure(facts.Message ?? "Kaynak bilgisi okunamadı.");

        IReadOnlyList<ContainerUsage>? containers = null;
        if (facts.Data.Containers is { } rows)
        {
            var context = await PanelResourceContextLoader.LoadAsync(_deployments, _services, serverId, cancellationToken);
            containers = MapContainers(rows, context);
        }

        return ServiceResult<ResourceOverview>.Success(Build(facts.Data, containers, includeDocker, _timeProvider.GetUtcNow().UtcDateTime));
    }

    public async Task<ServiceResult<DiskUsageReport>> ScanDiskAsync(Guid serverId, string? path, CancellationToken cancellationToken = default)
    {
        var normalized = ResourceRules.NormalizePath(path);
        if (!ResourceRules.IsValidScanPath(normalized))
            return ServiceResult<DiskUsageReport>.Failure("Geçersiz yol. Mutlak bir klasör yolu girin (/proc, /sys ve /dev taranamaz).");

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<DiskUsageReport>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await _inspector.ScanDiskAsync(connection.Data.Context, normalized, cancellationToken);
    }

    internal static IReadOnlyList<ContainerUsage> MapContainers(IReadOnlyList<ContainerUsageFact> rows, PanelResourceContext context) =>
        rows.Select(row =>
            {
                row.Labels.TryGetValue(PanelOwnership.ComposeProjectLabel, out var compose);
                return new ContainerUsage(row.Stats, PanelOwnership.Resolve(row.Stats.Name, row.Labels, context), string.IsNullOrEmpty(compose) ? null : compose);
            })
            .OrderByDescending(c => c.Stats.CpuPercent)
            .ThenByDescending(c => c.Stats.MemoryUsageBytes)
            .ToList();

    internal static ResourceOverview Build(ResourceFacts facts, IReadOnlyList<ContainerUsage>? containers, bool includeDocker, DateTime collectedAt) =>
        new()
        {
            Snapshot = facts.Snapshot,
            Processes = facts.Processes,
            Storage = facts.Storage,
            DockerIncluded = includeDocker,
            Containers = containers,
            DockerMessage = facts.DockerMessage,
            Findings = ResourceHeuristics.Evaluate(facts.Snapshot, facts.Processes, facts.Storage, containers),
            CollectedAt = collectedAt
        };
}

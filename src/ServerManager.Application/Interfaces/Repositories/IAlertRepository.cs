using ServerManager.Application.Alerting;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Interfaces.Repositories;

public interface IAlertRepository
{
    Task<IReadOnlyList<AlertRule>> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default);

    Task<AlertRule?> GetRuleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> RuleNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);

    void RemoveRuleChannels(IEnumerable<AlertRuleChannel> links);

    Task<IReadOnlyDictionary<Guid, int>> GetFiringCountsByRuleAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync(CancellationToken cancellationToken = default);

    Task<NotificationChannel?> GetChannelAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ChannelNameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetExistingChannelIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task AddChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetRuleCountsByChannelAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AlertEvent>> SearchEventsAsync(AlertEventFilterDto filter, CancellationToken cancellationToken = default);

    Task<AlertEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEvent>> GetOpenEventsAsync(CancellationToken cancellationToken = default);

    Task AddEventAsync(AlertEvent alert, CancellationToken cancellationToken = default);

    Task<(int Firing, int Critical)> CountFiringAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEvent>> GetLatestFiringAsync(int count, CancellationToken cancellationToken = default);

    Task AddDeliveryAsync(NotificationDelivery delivery, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDelivery>> GetRecentDeliveriesAsync(int count, CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredDeliveriesAsync(DateTime cutoffUtc, int keepLatestPerChannel, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertServerSnapshot>> GetServerSnapshotsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricSample>> GetMetricSamplesAsync(DateTime since, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertUptimeSnapshot>> GetUptimeSnapshotsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertSslSnapshot>> GetSslSnapshotsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertDeploymentSnapshot>> GetLatestFinishedDeploymentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Silinmemiş her yedekleme işinin son biten (başarılı/başarısız) yedeği.</summary>
    Task<IReadOnlyList<AlertBackupSnapshot>> GetLatestFinishedBackupsAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

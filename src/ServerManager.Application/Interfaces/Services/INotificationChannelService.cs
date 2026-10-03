using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;

namespace ServerManager.Application.Interfaces.Services;

public interface INotificationChannelService
{
    Task<IReadOnlyList<NotificationChannelListItemDto>> GetChannelsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChannelOptionDto>> GetChannelOptionsAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<NotificationProviderDto> GetProviders();

    Task<ServiceResult<NotificationChannelFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(NotificationChannelFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(NotificationChannelFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult> SendTestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDeliveryDto>> GetRecentDeliveriesAsync(int count, CancellationToken cancellationToken = default);
}

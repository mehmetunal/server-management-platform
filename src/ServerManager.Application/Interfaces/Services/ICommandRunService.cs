using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Commands;

namespace ServerManager.Application.Interfaces.Services;

public interface ICommandRunService
{
    Task<PagedResult<CommandRunListItemDto>> SearchAsync(CommandRunFilterDto filter, CancellationToken cancellationToken = default);

    Task<ServiceResult<CommandRunDetailsDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Kaydı ve hedefleri oluşturur; komut henüz çalışmaz.</summary>
    Task<ServiceResult<Guid>> BeginAsync(CommandRunRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Hedeflerde komutu paralel çalıştırır ve sonuçları kaydeder.</summary>
    Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Uygulama yeniden başladığında yarım kalan kayıtları "kesildi" olarak işaretler.</summary>
    Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default);
}

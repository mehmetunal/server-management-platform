using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Templates;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Interfaces.Services;

public interface IServerTemplateService
{
    Task<IReadOnlyList<ServerTemplateListItemDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerTemplateOptionDto>> GetOptionsAsync(ServerTemplateKind? kind = null, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServerTemplateFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(ServerTemplateFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(ServerTemplateFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

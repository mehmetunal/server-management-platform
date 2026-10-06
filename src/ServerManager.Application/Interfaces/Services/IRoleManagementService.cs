using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Roles;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>
/// Roller ve izinleri. SuperAdmin değişmez (her izne sahiptir). Diğer yerleşik roller düzenlenebilir ve varsayılana
/// döndürülebilir; özel roller oluşturulabilir, kopyalanabilir ve silinebilir. İzin değişikliğinde etkilenen kullanıcıların
/// oturumları yenilenir.
/// </summary>
public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleOptionDto>> GetRoleOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Yeni rol formu; <paramref name="cloneFromId"/> verilirse o rolün izinleriyle doldurulur.</summary>
    Task<ServiceResult<RoleEditorDto>> GetForCreateAsync(Guid? cloneFromId, CancellationToken cancellationToken = default);

    Task<ServiceResult<RoleEditorDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<Guid>> CreateAsync(RoleFormDto dto, CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateAsync(RoleFormDto dto, CancellationToken cancellationToken = default);

    /// <summary>Yerleşik rolün izinlerini varsayılana (çekirdek + eklenti önerileri) döndürür.</summary>
    Task<ServiceResult> ResetToDefaultAsync(Guid id, bool confirmSelfLockout, CancellationToken cancellationToken = default);

    /// <summary>Özel rolü siler. Rolde kullanıcı varsa <paramref name="reassignToRoleId"/> verilmelidir; kullanıcılar o role taşınır.</summary>
    Task<ServiceResult> DeleteAsync(Guid id, Guid? reassignToRoleId, CancellationToken cancellationToken = default);
}
